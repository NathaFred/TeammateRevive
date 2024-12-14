namespace TeammateRevive.Revive.Rules
{
    public class ReviveRuleValues
    {
        // totem range
        public float BaseTotemRange { get; set; } = 30;
        public float IncreaseRangeWithPlayersFactor { get; set; } = .4f;
        public float ItemIncreaseRangeFactor { get; set; } = .5f;
        
        // revive
        public float ReviveTimeSeconds { get; set; } = 6;
        public float ReduceReviveProgressFactor { get; set; } = .05f;
        public float ReviveLinkBuffTimeFactor { get; set; } = 1f;
        public float ObolReviveFactor { get; set; } = 1.125f;
        public float ObolDamageReduceFactor { get; set; } = 1.1f;
        public float PostReviveRegenDurationSec { get; set; } = 4f;
        public float PostReviveRegenFraction { get; set; } = .4f;
        
        
        // curse
        public float BaseReduceHpFactor { get; set; } = .25f;
        public float ReduceHpFactor { get; set; } = 1.2f;

        public bool ForceDeathCurseRule { get; set; } = false;

        public bool DebugKeepTotem { get; set; } = false;

        public bool ForceEnableDeathCurseForSinglePlayer { get; set; } = false;

        public bool EnableRevivalToken { get; set; } = true;
        
        public bool CutReviveeHp { get; set; } = true;

        public float DeathCurseChance { get; set; } = 66f;

        public float ReviverDeathCurseChance { get; set; } = 66f;

        public bool RequireHitboxesActive { get; set; } = false;

        public ReviveRuleValues Clone()
        {
            return (ReviveRuleValues)MemberwiseClone();
        }
    }
}