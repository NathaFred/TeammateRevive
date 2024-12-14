using System.Collections.Generic;
using System.Linq;
using RoR2;
using TeammateRevive.Common;
using TeammateRevive.Content;
using TeammateRevive.Logging;
using TeammateRevive.Players;
using TeammateRevive.ProgressBar;
using TeammateRevive.Revive.Rules;
using TeammateRevive.DeathTotem;
using TeammateRevive.Localization;
using UnityEngine;

namespace TeammateRevive.Revive
{
    public class RevivalTracker
    {
        public static RevivalTracker instance;
        public static readonly string[] IgnoredStages = { "bazaar" };

        private readonly PlayersTracker players;
        private readonly RunTracker run;
        private readonly ReviveRules rules;
        private readonly DeathTotemTracker deathTotemTracker;
        private readonly ReviveProgressBarTracker reviveProgressBarTracker;

        public RevivalTracker(PlayersTracker players, RunTracker run, ReviveRules rules, DeathTotemTracker deathTotemTracker, ReviveProgressBarTracker reviveProgressBarTracker)
        {
            instance = this;
            this.players = players;
            this.run = run;
            this.rules = rules;
            this.deathTotemTracker = deathTotemTracker;
            this.reviveProgressBarTracker = reviveProgressBarTracker;

            this.players.OnPlayerDead += OnPlayerDead;
            this.players.OnPlayerAlive += OnPlayerAlive;

            RoR2.Stage.onStageStartGlobal += OnStageStart;
        }

        #region Event handlers

        void OnPlayerDead(Player player)
        {
            player.ClearReviveLinks();
        }
        
        void OnPlayerAlive(Player player)
        {
            foreach (var otherPlayer in players.All)
            {
                otherPlayer.RemoveReviveLink(player);
            }
        }

        void OnStageStart(Stage self)
        {
            var sceneName = self.sceneDef.cachedName;
            Log.Debug($"Stage start: {self.sceneDef.cachedName}");
            deathTotemTracker.Clear();
            
            foreach (var player in players.All)
            {
                player.ClearReviveLinks();
            }

            if (NetworkHelper.IsClient() || IgnoredStages.Contains(sceneName) || !run.IsDeathCurseEnabled)
                return;

            foreach (var networkUser in NetworkUser.readOnlyInstancesList)
            {
                RemoveReduceHpItem(networkUser);
                networkUser.master.inventory?.RemoveItem(RevivalToken.Index);
            }
        }

        
        #endregion Event handlers
        
        public void Update()
        {
            // nothing to do if run didn't start yet
            if (!run.IsStarted) return;
            
            // for client, we'll need to update progress bar display only
            if (NetworkHelper.IsClient())
            {
                reviveProgressBarTracker.Update();
                return;
            }

            // if players didn't finish setup yet, we cannot do any updates
            if (!players.Setup) return;
            
            UpdatePlayersGroundPosition();

            // interactions between dead and alive players
            for (var deadIdx = 0; deadIdx < players.Dead.Count; deadIdx++)
            {
                var dead = players.Dead[deadIdx];
                var deathTotem = dead.deathTotem;

                if (deathTotem == null)
                {
                    Log.Warn($"Death Totem is missing {deadIdx}");
                    continue;
                }
                
                //have they been revived by other means?
                if (dead.CheckAlive() && !rules.Values.DebugKeepTotem)
                {
                    Log.Info("Removing totem revived by other means");
                    players.PlayerAlive(dead);
                    continue;
                }
                var totalReviveSpeed = 0f;
                var playersInRange = 0;

                var insidePlayersHash = deathTotem.GetInsidePlayersHash();
                var actualRange = rules.CalculateDeathTotemRadius(dead);

                // ReSharper disable once ForCanBeConvertedToForeach - array can be changed during iteration
                for (var aliveIdx = 0; aliveIdx < players.Alive.Count; aliveIdx++)
                {
                    var reviver = players.Alive[aliveIdx];
                    if (reviver.CheckDead()) continue;

                    var playerBody = reviver.GetBody();
                    var hasReviveEverywhere = playerBody.inventory.GetItemCount(DeadMansHandItem.Index) > 0;
                    var inRange = hasReviveEverywhere || Vector3.Distance(playerBody.transform.position, deathTotem.transform.position) < (actualRange * .5);
                    var isHitboxValid = !rules.Values.RequireHitboxesActive || !playerBody.disablingHurtBoxes;//REQUIRES PUBLICIZED ASSEMBLY
                    if (inRange && isHitboxValid)
                    {
                        playersInRange++;
                        
                        // player entered range, update players in range list
                        if (!deathTotem.insidePlayerIDs.Contains(playerBody.netId))
                            deathTotem.insidePlayerIDs.Add(playerBody.netId);
                        
                        // revive progress
                        var reviveSpeed = rules.GetReviveSpeed(reviver, deathTotem.insidePlayerIDs.Count);
                        totalReviveSpeed += reviveSpeed;
                        dead.reviveProgress += reviveSpeed * Time.deltaTime;
                        dead.reviveProgress = Mathf.Clamp01(dead.reviveProgress);
                        
                        // if player in range, update revive revive links
                        reviver.IncreaseReviveLinkDuration(dead, Time.deltaTime + Time.deltaTime  / rules.Values.ReduceReviveProgressFactor * rules.Values.ReviveLinkBuffTimeFactor);

                        DamageReviver(playerBody, dead);
                    }
                    else
                    {
                        // player left the range
                        if (deathTotem.insidePlayerIDs.Contains(playerBody.netId))
                            deathTotem.insidePlayerIDs.Remove(playerBody.netId);
                    }
                }
                
                if (dead.reviveProgress >= 1)
                {
                    Revive(dead);
                    continue;
                }

                deathTotemTracker.UpdateTotem(dead, insidePlayersHash, playersInRange, totalReviveSpeed);
            }
            
            // update revive link buffs
            // NOTE: revive links are tracked in Normal Mode, but no buff is displayed
            if (run.IsDeathCurseEnabled) 
                UpdateReviveLinkBuffs();
            
            // progress bar
            reviveProgressBarTracker.Update();
        }

        void Revive(Player dead)
        {
            var linkedPlayers = players.Alive
                .Where(p => p.IsLinkedTo(dead))
                .ToArray();

            ScheduleCutReviveeHp(dead);
            players.Respawn(dead);

            // add Death Curse to every linked character
            if (run.IsDeathCurseEnabled)
            {
                ApplyDeathCurses(dead, linkedPlayers);
            }
            
            // remove revive links from all players
            foreach (var player in players.All) 
                player.RemoveReviveLink(dead);
            
            // add post-revive regeneration to revivers
            if (rules.Values.PostReviveRegenDurationSec != 0)
            {
                foreach (var player in linkedPlayers)
                    player.GetBody().AddTimedBuff(ReviveRegen.Index, rules.Values.PostReviveRegenDurationSec);
            }
        }

        private void ApplyDeathCurses(Player dead, Player[] linkedPlayers)
        {
            // invert - from "chance to get curse" to "chance to avoid curse"
            var reviveeChance = Mathf.Clamp(100 - rules.Values.DeathCurseChance, 0, 100);
            var reviverChance = Mathf.Clamp(100 - rules.Values.ReviverDeathCurseChance, 0, 100);
            
            bool RollCurse(Player player, float chance, float extraLuck)
            {
                // negative luck - take largest roll value
                // if value > chance - roll is failed
                // therefore positive luck - outcome is more likely
                // using inverted roll, so clover sound effect will be triggered if clover is present
                var luckTotal = extraLuck + player.master.master.luck;
                var curseAvoided = Util.CheckRoll(chance, luckTotal, player.master.master.luck > 0 ? player.master.master : null);
                
                Log.Info($"Rolled curse for {player.networkUser.userName}: {chance:F1}% (luck: {player.master.master.luck:F0}+{extraLuck:F0}). Success: {curseAvoided}");
                if (!curseAvoided)
                {
                    player.GiveItem(DeathCurse.ItemIndex);
                    Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                    {
                        baseToken = LanguageConsts.TEAMMATE_REVIVAL_UI_CURSED,
                        paramTokens = new[] { player.networkUser.userName, $"{luckTotal:F0}" }
                    });
                    return true;
                }
                
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage
                {
                    baseToken = LanguageConsts.TEAMMATE_REVIVAL_UI_AVOIDED_CURSE,
                    paramTokens = new[] { player.networkUser.userName, $"{luckTotal:F0}" }
                });
                return false;
            }
            
            // roll for revivee
            RollCurse(dead, reviveeChance, dead.ItemCount(RevivalToken.Index));
            
            // the more time spent reviving this player - the greater chance to receive a curse
            var array = SortByLinkDuration(linkedPlayers, dead).ToArray();
            for (var index = 0; index < array.Length; index++)
            {
                var player = array[index];
                if (RollCurse(player, reviverChance, index))
                {
                    // when at least one player received curse, stop rolling for other members
                    break;
                }
            }

            if (rules.Values.EnableRevivalToken)
            {
                dead.GiveItem(RevivalToken.Index);
            }
        }

        IEnumerable<Player> SortByLinkDuration(Player[] linkedPlayers, Player dead)
        {
            var totalTime = linkedPlayers.Sum(p => p.GetReviveLinkDuration(dead));
            var p = 0f;
            return linkedPlayers
                .Select(player =>
                {
                    var fraction = p + player.GetReviveLinkDuration(dead) / totalTime;
                    p = fraction;
                    return new { player, fraction };
                })
                .OrderBy(t => t.fraction)
                .Select(t => t.player);
        }

        private void DamageReviver(CharacterBody playerBody, Player dead)
        {
            // special case fot Transcendence - damage shield instead of HP
            if (playerBody.inventory.GetItemCount(RoR2Content.Items.ShieldOnly) > 0)
            {
                playerBody.healthComponent.Networkshield = CalcDamageResult(
                    playerBody.maxShield,
                    playerBody.healthComponent.shield,
                    0.1f,
                    dead,
                    playerBody.inventory.GetItemCount(DeadMansHandItem.Index)
                );
            }
            else
            {
                playerBody.healthComponent.Networkhealth = CalcDamageResult(
                    playerBody.maxHealth,
                    playerBody.healthComponent.health,
                    0.05f,
                    dead,
                    playerBody.inventory.GetItemCount(DeadMansHandItem.Index)
                );
            }

            // prevent recharging shield and other "out of combat" stuff like Red Whip during reviving
            if (playerBody.outOfDangerStopwatch > 3) playerBody.outOfDangerStopwatch = 3;
        }

        float CalcDamageResult(float max, float current, float dmgThreshold, Player dead, int reviverReviveEverywhereCount)
        {
            var damageSpeed = rules.GetDamageSpeed(max, dead, reviverReviveEverywhereCount);
            var damageAmount = damageSpeed * Time.deltaTime;
            
            var minValue = max * dmgThreshold;
            if (current < minValue)
                return current;
                
            return Mathf.Clamp(
                current - damageAmount,
                minValue,
                max
            );
        }
        void UpdatePlayersGroundPosition()
        {
            foreach (var player in players.Alive)
            {
                if (player.GetBody() == null)
                {
                    continue;
                }
                player.UpdateGroundPosition();
            }
        }
        

        void RemoveReduceHpItem(NetworkUser networkUser)
        {
            if (NetworkHelper.IsClient()) return;

            Log.DebugMethod();
            var userName = networkUser.userName;
            var inventory = networkUser.master?.inventory;

            if (inventory == null)
            {
                Log.Warn($"Player has no inventory! {userName}");
                return;
            }

            var reduceHpItemCount = inventory.GetItemCount(DeathCurse.ItemIndex);
            inventory.RemoveItem(DeathCurse.ItemIndex, inventory.GetItemCount(CharonsObol.Index) + 1);
            Log.Info(
                $"Removed reduce HP item for ({userName}). Was {reduceHpItemCount}. Now: {inventory.GetItemCount(DeathCurse.ItemIndex)}");
        }

        void UpdateReviveLinkBuffs()
        {
            foreach (var player in players.Alive)
            {
                var characterBody = player.GetBody();
                if (characterBody == null) continue;
                characterBody.SetBuffCount(ReviveLink.Index, player.GetPlayersReviveLinks());
            }
        }

        void ScheduleCutReviveeHp(Player player)
        {
            void Callback(CharacterBody body)
            {
                if (player.BodyId != body.netId) return;
                
                CutReviveeHp(body);
                CharacterBody.onBodyStartGlobal -= Callback;
            }
            CharacterBody.onBodyStartGlobal += Callback;
        }

        void CutReviveeHp(CharacterBody body)
        {
            var hpWas = body.healthComponent.Networkhealth;
            var effectiveHp = (body.maxHealth + body.maxShield) * .4f;
            body.healthComponent.Networkhealth = Mathf.Clamp(effectiveHp, 1, body.maxHealth);
            body.healthComponent.Networkshield = Mathf.Clamp(body.maxHealth - effectiveHp, 0, body.maxShield);
            Log.DebugMethod($"Prev hp: {hpWas}; Now: {body.healthComponent.health}");
        }
    }
}