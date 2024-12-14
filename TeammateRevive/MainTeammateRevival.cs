using System;
using System.Collections;
using BepInEx;
using R2API;
using R2API.Networking;
using R2API.Utils;
using RoR2;
using TeammateRevive.Artifact;
using TeammateRevive.Common;
using TeammateRevive.Configuration;
using TeammateRevive.Debugging;
using TeammateRevive.Integrations;
using TeammateRevive.Logging;
using TeammateRevive.Players;
using TeammateRevive.ProgressBar;
using TeammateRevive.Resources;
using TeammateRevive.Revive;
using TeammateRevive.Revive.Rules;
using TeammateRevive.DeathTotem;
using TeammateRevive.Localization;
using UnityEngine;
using UnityEngine.Networking;

namespace TeammateRevive
{
    [BepInDependency("dev.ontrigger.itemstats", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.xoxfaby.BetterUI", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.KingEnderBrine.InLobbyConfig", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(RiskOfOptionsIntegration.Guid, BepInDependency.DependencyFlags.SoftDependency)]

    [BepInDependency("com.bepis.r2api")]
    [BepInDependency(R2API.ItemAPI.PluginGUID)]
    [BepInDependency(R2API.PrefabAPI.PluginGUID)]
    [BepInDependency(R2API.Networking.NetworkingAPI.PluginGUID)]
    [BepInDependency(R2API.LanguageAPI.PluginGUID)]
    [BepInDependency(R2API.ContentManagement.R2APIContentManager.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class MainTeammateRevival : BaseUnityPlugin
    {
        #region Plugin variables
        
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "KosmosisDire";
        public const string PluginName = "TeammateRevival";
        public const string PluginVersion = "4.2.1";
        
        #endregion
        
        public static MainTeammateRevival instance;

        private DeathCurseArtifact deathCurseArtifact;

        public PluginConfig pluginConfig;
        private PlayersTracker players;
        private RunTracker run;
        private RevivalTracker revivalTracker;
        
        private InLobbyConfigIntegration inLobbyConfigIntegration;
        private RiskOfOptionsIntegration riskOfOptionsIntegration;
        
        private ConsoleCommands consoleCommands;
        private ReviveRules rules;
        private ReviveLinkBuffIconManager linkBuffIconManager;
        private ReviveLongRangeActivationManager reviveLongRangeActivationManager;
        private DeathTotemTracker deathTotemTracker;
        private ProgressBarController progressBarController;
        private ReviveProgressBarTracker progressBarTracker;
        private ItemDropManager itemDropManager;
        private ContentManager contentManager;

        #region Setup

        public void Awake()
        {
            instance = this;
            pluginConfig = PluginConfig.Load(Config);
            LanguageManager.RegisterLanguages();
            
            NetworkingAPI.RegisterMessageType<SyncDeathTotemMessage>();
            NetworkingAPI.RegisterMessageType<SetRulesMessage>();

            deathCurseArtifact = new DeathCurseArtifact();
            run = new RunTracker(deathCurseArtifact);
            players = new PlayersTracker(run, pluginConfig);
            rules = new ReviveRules(run, pluginConfig);
            deathTotemTracker = new DeathTotemTracker(players, run, rules);
            progressBarController = new ProgressBarController();
            progressBarTracker = new ReviveProgressBarTracker(progressBarController, players,
                deathTotemTracker, rules);
            revivalTracker = new RevivalTracker(players, run, rules, deathTotemTracker, progressBarTracker);
        
            consoleCommands = new ConsoleCommands(rules, pluginConfig);
            linkBuffIconManager = new ReviveLinkBuffIconManager();
            inLobbyConfigIntegration = new InLobbyConfigIntegration(pluginConfig);
            riskOfOptionsIntegration = new RiskOfOptionsIntegration(pluginConfig);
            reviveLongRangeActivationManager = new ReviveLongRangeActivationManager(run, deathTotemTracker);
            itemDropManager = new ItemDropManager(run, rules);
            contentManager = new ContentManager(rules, run, deathCurseArtifact);
            
            Log.Init(pluginConfig, Logger);
            ReviveHelper.Init();
            CustomResources.LoadCustomResources();
            HideDeathCurseContent.Init(pluginConfig);
            contentManager.Init();
            rules.ApplyConfigValues();
#if DEBUG
            DebugHelper.Init(pluginConfig);
#endif
            run.RunStarted += OnRunStarted;
            run.RunEnded += OnRunEnded;
            
            SetupHooks();

            Log.Debug("Setup Teammate Revival");
        }

        void SetupHooks()
        {
            On.RoR2.Run.BeginGameOver += Hook_BeginGameOver;
            On.RoR2.Run.AdvanceStage += Hook_AdvanceStage;
            On.RoR2.NetworkUser.OnStartLocalPlayer += Hook_OnStartLocalPlayer;
        }

        #endregion

        #region Hooks

        void Hook_OnStartLocalPlayer(On.RoR2.NetworkUser.orig_OnStartLocalPlayer orig, NetworkUser self)
        {
            orig(self);

            if (NetworkHelper.IsClient())
            {
                ClientScene.RegisterPrefab(CustomResources.DeathTotem.gameObject);
                FindObjectOfType<NetworkManager>().spawnPrefabs.Add(CustomResources.DeathTotem.gameObject);
                
                Log.Info("Client registered prefabs");
                return;
            }

            NetworkManager.singleton.connectionConfig.DisconnectTimeout = 5;
            NetworkManager.singleton.connectionConfig.MaxSentMessageQueueSize = 1024;
        }

        void Hook_BeginGameOver(On.RoR2.Run.orig_BeginGameOver orig, Run self, GameEndingDef gameEndingDef)
        {
            orig(self, gameEndingDef);

            if (NetworkHelper.IsClient()) return;

            Log.Info("Game Over - reseting data");

            players.Reset();
            run.IsStarted = false;
        }

        void Hook_AdvanceStage(On.RoR2.Run.orig_AdvanceStage orig, Run self, SceneDef nextScene)
        {
            orig(self, nextScene);

            if (NetworkHelper.IsClient()) return;

            Log.Info("Advanced a stage - now resetting");
            players.Reset();
        }
        
        #endregion

        public void Update()
        {
#if DEBUG
            DebugHelper.Update();
#endif
            players.Update();
            revivalTracker.Update();
        }

        void OnRunStarted(RunTracker obj)
        {
            deathCurseArtifact.EnsureEnabled(rules);
        }

        private void OnRunEnded(RunTracker obj)
        {
            players.Reset();
        }

        public Func<IEnumerator, Coroutine> DoCoroutine => StartCoroutine;
    }
}
