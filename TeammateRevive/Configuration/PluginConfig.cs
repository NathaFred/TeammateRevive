using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using TeammateRevive.Revive.Rules;

namespace TeammateRevive.Configuration
{
    public class PluginConfig
    {
        public bool ConsoleLogging { get; set; }
        public bool ChatLogging { get; set; }

        public LogLevel LogLevel { get; set; }

        public ServerLoggingConfig ServerLogging { get; private set; } = new();
        public bool FileLogging { get; set; }
        public string FileLoggingPath { get; set; }
        public bool GodMode { get; set; }

        public bool HideDeathCurseItemsInLogBook { get; private set; }
        public bool HideDeathCurseArtifact { get; private set; }

        public ReviveRuleValues RuleValues { get; } = new();

        public BindCollection RuleValuesBindCollection { get; private set; }
        public BindCollection DebugBindCollection { get; private set; }
        public BindCollection MiscBindCollection { get; private set; }


        public static PluginConfig Load(ConfigFile config)
        {
            var pluginConfig = new PluginConfig();
            
            pluginConfig.RuleValuesBindCollection = BindRuleValues(config, pluginConfig.RuleValues);
            pluginConfig.DebugBindCollection = BindDebugSection(config, pluginConfig);
            pluginConfig.ServerLogging = ReadServerLoggingConfig(config);
            pluginConfig.MiscBindCollection = BindMisc(config, pluginConfig);

            return pluginConfig;
        }

        private static ServerLoggingConfig ReadServerLoggingConfig(ConfigFile configFile)
        {
            var config = new ServerLoggingConfig();

            configFile.BindCollection("Server logging")
                .Bind(
                    key: "Use external logging server",
                    description: "Log debugging messages to external logging server.",
                    set: v => config.IsEnabled = v,
                    defaultValue: false)
                .Bind(
                    key: "Logging server URL",
                    description: "Logging server URL",
                    set: v => config.Url = v,
                    defaultValue: "")
                .Bind(
                    key: "Logging server user name",
                    description: "User name that will be used for logging server",
                    set: v => config.UserName = v,
                    defaultValue: ""
                )
                .Bind(
                    key: "Logging server room",
                    description: "Room name that will be used for logging server",
                    set: v => config.RoomName = v,
                    defaultValue: ""
                )
                .Bind(
                    key: "Log all",
                    description: "Include all application logs, not only for this plugin",
                    set: v => config.LogAll = v,
                    defaultValue: false
                );
            
            return config;
        }

        static BindCollection BindDebugSection(ConfigFile configFile, PluginConfig pluginConfig)
        {
            return configFile.BindCollection("Debugging")
                .Bind(
                    key: "Keep death totem for characters revived by other means",
                    description: "Debug-only function that will ruin your experience when using Dio's Best Friend",
                    set: v => pluginConfig.RuleValues.DebugKeepTotem = v,
                    defaultValue: false)
                .Bind(
                    key: "Force enable Death Curse even for single player",
                    description: "Retain Death Curse and related items even in single player",
                    set: v => pluginConfig.RuleValues.ForceEnableDeathCurseForSinglePlayer = v,
                    defaultValue: false)
                
                // Thunderstore is having troubles displaying proper dropdowns, so I need to do this :/
                .Bind(
                    key: "Log Level",
                    configDescription: new ConfigDescription("How much logs to display",
                        new AcceptableValueList<string>(Enum.GetNames(typeof(LogLevel)))),
                    set: v => pluginConfig.LogLevel = (LogLevel)Enum.Parse(typeof(LogLevel), v),
                    defaultValue: LogLevel.Info.ToString("G"))
                
                .Bind(
                    key: "Console Logging",
                    description: "Log debugging messages to the console",
                    set: v => pluginConfig.ConsoleLogging = v,
                    defaultValue: true)
                .Bind(
                    key: "Chat Logging",
                    description: "Log debugging messages to the in-game chat.",
                    set: v => pluginConfig.ChatLogging = v,
                    defaultValue: false)
                .Bind(
                    key: "File Logging",
                    description:
                    "Log debugging messages to log.txt located on the desktop by default (sometimes the path cannot be found, so set a custom path below). If the path cannot be found it will write to \"C:\\log.txt\" instead.",
                    set: v => pluginConfig.FileLogging = v,
                    defaultValue: false)
                .Bind(
                    key: "File Logging Path",
                    description:
                    "This sets the location that the logging file will be created. Leave blank to put log.txt on the desktop. If the log file is not showing up set your path manually here.",
                    set: v => pluginConfig.FileLoggingPath = v,
                    defaultValue: "")
                .Bind(
                    key: "God Mode",
                    description:
                    "Super massive base damage, and super speed for the host. For testing purposes only, Makes the game incredibly boring.",
                    set: v => pluginConfig.GodMode = v,
                    defaultValue: false);
        }
        
        static BindCollection BindRuleValues(ConfigFile configFile, ReviveRuleValues values)
        {
            return configFile.BindCollection("Revive rules configuration")
                .Bind(
                    key: "Base revive range",
                    description: "How wide revive circle will be",
                    set: v => values.BaseTotemRange = v,
                    defaultValue: values.BaseTotemRange,
                    metadata: new FloatMetadata(0, 300))
                .Bind(
                    key: "Increase range per player factor",
                    description: "How much to increase revive circle range per player. Set to 0 to disable. " +
                                 "\nRangeIncrease = BaseRange * IncreasePerPlayerFactor",
                    set: v => values.IncreaseRangeWithPlayersFactor = v,
                    defaultValue: values.IncreaseRangeWithPlayersFactor,
                    metadata: new FloatMetadata(0, 2, .01f))
                .Bind(
                    key: "Increase range per Obol",
                    description: "[Only with Death Curse enabled] How much to increase revive circle range per dead character's Obol. " +
                                 "\nnRangeIncrease = BaseRange * ItemsCount * IncreasePerPlayerFactor",
                    set: v => values.ItemIncreaseRangeFactor = v,
                    defaultValue: values.ItemIncreaseRangeFactor,
                    metadata: new FloatMetadata(0, 2, .01f))
                .Bind(
                    key: "Revive time",
                    description: "How much time one player will need to revive one dead character.",
                    set: v => values.ReviveTimeSeconds = v,
                    defaultValue: values.ReviveTimeSeconds,
                    metadata: new FloatMetadata(0, 30))
                .Bind(
                    key: "Reduce revive progress factor",
                    description: "How fast revive progress will be decreased when no one in revive range. " +
                                 "\nReduceSpeed = -(1 / ReviveTime * ReduceProgressFactor)",
                    set: v => values.ReduceReviveProgressFactor = v,
                    defaultValue: values.ReduceReviveProgressFactor,
                    metadata: new FloatMetadata(0, 1, .01f))
                .Bind(
                    key: "Revive Link buff duration factor",
                    description: "How long Revive Link buff will stay after player leave revive range. " +
                                 "\nBuffTime = TimeInCircle / ReduceReviveProgressFactor * ReviveLinkBuffTimeFactor + 1 second" +
                                 "\nBasically, when 1 - buff will be applied for exactly as long as it will take to remove all added revive progress (+1 second)." +
                                 "\nSo, when 0.5 - only half of that.",
                    set: v => values.ReviveLinkBuffTimeFactor = v,
                    defaultValue: values.ReviveLinkBuffTimeFactor,
                    metadata: new FloatMetadata(0, 5, .1f))
                .Bind(
                    key: "Obol revive factor",
                    description: "[Only with Death Curse enabled] How much revive speed will increase per dead character's Obol. " +
                                 "\nSpeedIncrease = ReviveTime / (ReviveTime / ObolReviveFactor ^ ObolsCount)",
                    set: v => values.ObolReviveFactor = v,
                    defaultValue: values.ObolReviveFactor,
                    metadata: new FloatMetadata(1, 3, .001f))
                .Bind(
                    key: "Obol damage reduce factor",
                    description: "[Only with Death Curse enabled] How much revive damage in revive circle will decrease per dead character's Obol. " +
                                 "\nDamagePercentage = 1 / (ObolDamageReduceFactor ^ DeadPlayerObolsCount)",
                    set: v => values.ObolDamageReduceFactor = v,
                    defaultValue: values.ObolDamageReduceFactor,
                    metadata: new FloatMetadata(1, 3, .01f))
                .Bind(
                    key: "Base Death Curse HP reduce factor",
                    description: "[Only with Death Curse enabled] For how much HP will be reduced for Death Curse." +
                                 "\nReduceHP = MaxHP - (MaxHP / (ReduceHpFactor ^ CursesCount + BaseReduceHpFactor))" +
                                 "\nMaxHP = MaxHP - ReduceHP",
                    set: v => values.BaseReduceHpFactor = v,
                    defaultValue: values.BaseReduceHpFactor,
                    metadata: new FloatMetadata(0, 1, .01f))
                .Bind(
                    key: "Death Curse HP reduce factor",
                    description: "[Only with Death Curse enabled] For how much HP will be reduced for Death Curse." +
                                 "\nReduceHP = MaxHP - (MaxHP / (ReduceHpFactor ^ CursesCount + BaseReduceHpFactor))" +
                                 "\nMaxHP = MaxHP - ReduceHP",
                    set: v => values.ReduceHpFactor = v,
                    defaultValue: values.ReduceHpFactor,
                    metadata: new FloatMetadata(0, 3, .1f))
                .Bind(
                    key: "Force Death Curse rule",
                    description: "Force enable Death Curse logic (e.g. Artifact of Death Curse) even when artifact is disabled.",
                    set: v => values.ForceDeathCurseRule = v,
                    defaultValue: values.ForceDeathCurseRule)
                .Bind(
                    key: "Enable Revival Token",
                    description: "[Only with Death Curse enabled] If enabled, player that is revived a lot will be less likely to receive Death Curse later.",
                    set: v => values.EnableRevivalToken = v,
                    defaultValue: values.EnableRevivalToken)
                .Bind(
                    key: "Cut Revivee HP",
                    description: "If enabled, revived player will get hp/shield cut to 40%",
                    set: v => values.CutReviveeHp = v,
                    defaultValue: values.CutReviveeHp)
                .Bind(
                    key: "Death Curse chance",
                    description: "[Only with Death Curse enabled] Chance to receive Death Curse on revival for revivee (Range: 0-100%)",
                    set: v => values.DeathCurseChance = v,
                    defaultValue: values.DeathCurseChance,
                    metadata: new FloatMetadata(0, 100, 1))
                .Bind(
                    key: "Reviver Death Curse chance",
                    description: "[Only with Death Curse enabled] Chance to receive Death Curse on revival for reviver (Range: 0-100%)",
                    set: v => values.ReviverDeathCurseChance = v,
                    defaultValue: values.ReviverDeathCurseChance,
                    metadata: new FloatMetadata(0, 100, 1))
                .Bind(
                    key: "Post revive regeneration time",
                    description: "After reviving, 40% of revivee and linked revivers HP is restored. This value specify how long regeneration buff is active in seconds. If set to 0 - revive regen is disabled.",
                    set: v => values.PostReviveRegenDurationSec = v,
                    defaultValue: values.PostReviveRegenDurationSec,
                    metadata: new FloatMetadata(0, 30, 1)
                )
                .Bind(
                    key: "Require hitboxes active",
                    description: "Prevent players from reviving if their hitboxes are disabled via Strides of Heresy or other similar skills.",
                    set: v => values.RequireHitboxesActive = v,
                    defaultValue: false);
        }

        static BindCollection BindMisc(ConfigFile configFile, PluginConfig config)
        {
            return configFile.BindCollection("Misc")
                .Bind(key: "Hide Death Curse mode items in logbook",
                    description:
                    "Set it to true if you don't plan on playing Death Curse mode and don't want to see related items in logbook",
                    set: v => config.HideDeathCurseItemsInLogBook = v,
                    defaultValue: false,
                    new EntryMetadata(restartRequired: true))
                .Bind(key: "Hide Death Curse Artifact",
                    description:
                    "Set it to true if you don't plan on playing Death Curse mode and don't want to see Death Curse artifact in lobby",
                    set: v => config.HideDeathCurseArtifact = v,
                    defaultValue: false,
                    new EntryMetadata(restartRequired: true));
        }
    }
}