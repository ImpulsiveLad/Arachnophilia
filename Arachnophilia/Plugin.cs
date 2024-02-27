using Arachnophilia.Patches;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CSync.Lib;
using CSync.Util;
using HarmonyLib;
using System.Runtime.Serialization;

namespace Arachnophilia
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class Arachnophilia : BaseUnityPlugin
    {
        private const string modGUID = "impulse.Arachnophilia";
        private const string modName = "Arachnophilia";
        private const string modVersion = "1.6.0";
        private readonly Harmony harmony = new Harmony(modGUID);

        public ManualLogSource mls;

        public static Arachnophilia instance;

        public new static SyncConfig Config;
        void Awake()
        {
            instance = this;

            Config = new SyncConfig(base.Config);

            harmony.PatchAll(typeof(SandSpiderStartPatch));
            harmony.PatchAll(typeof(SandSpiderAttemptPlaceWebTrapPatch));
            harmony.PatchAll(typeof(SandSpiderState));
            harmony.PatchAll(typeof(SandSpiderUpdatePatch));
            harmony.PatchAll(typeof(SandSpiderUpdateTPatch));
            harmony.PatchAll(typeof(SandSpiderOnCollideWithPlayerPatch));
            harmony.PatchAll(typeof(SandSpiderGetWallPositionForSpiderMeshPatch));
            harmony.PatchAll(typeof(SandSpiderAIGrabBodyPatch));
            harmony.PatchAll(typeof(SyncConfig));

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);
        }
    }
    [DataContract]
    public class SyncConfig : SyncedConfig<SyncConfig>
    {
        [DataMember] public SyncedEntry<int> MinWebCount { get; private set; }
        [DataMember] public SyncedEntry<int> MaxWebCount { get; private set; }
        [DataMember] public SyncedEntry<float> MinWebLength { get; private set; }
        [DataMember] public SyncedEntry<float> MaxWebLength { get; private set; }
        [DataMember] public SyncedEntry<float> MinWebDistance { get; private set; }
        [DataMember] public SyncedEntry<float> SpiderSetupSpeed { get; private set; }
        [DataMember] public SyncedEntry<float> SpiderChaseSpeed { get; private set; }
        [DataMember] public SyncedEntry<float> WebTimeRange { get; private set; }
        [DataMember] public SyncedEntry<float> FailedWebTrapTime { get; private set; }
        [DataMember] public SyncedEntry<float> WebPlaceInterval { get; private set; }
        [DataMember] public SyncedEntry<int> SpiderDamage { get; private set; }
        [DataMember] public SyncedEntry<int> SpiderHp { get; private set; }
        [DataMember] public SyncedEntry<int> MinSpooledBodies { get; private set; }
        [DataMember] public SyncedEntry<int> MaxSpooledBodies { get; private set; }

        public SyncConfig(ConfigFile cfg) : base("Arachnophilia")
        {
            ConfigManager.Register(this);

            SpiderSetupSpeed = cfg.BindSyncedEntry("General",
            "Spider Setup Speed",
            1.5f,
            "Speed multiplier applied to the spider while it is not chasing a player.");
            SpiderChaseSpeed = cfg.BindSyncedEntry("General",
                        "Spider Chase Speed",
                        1.25f,
                        "Speed multiplier applied to the spider while it is chasing a player.");
            SpiderDamage = cfg.BindSyncedEntry("General",
                        "Spider Damage",
                        35,
                        "The damage dealt to players by the spider.");
            SpiderHp = cfg.BindSyncedEntry("General",
                        "Spider HP",
                        6,
                        "Health of the spider.");
            MinSpooledBodies = cfg.BindSyncedEntry("Features",
                        "Min Extra Cocoons",
                        3,
                        "The minimum number of bodies the spider spawns in and decorates its nest with during its setup phase. Set both min and max to 0 to disable. Keep in mind that this value should be higher than Min Web Count, excessively high values may result in lag, Min should be less than Max.");
            MaxSpooledBodies = cfg.BindSyncedEntry("Features",
                        "Max Extra Cocoons",
                        4,
                        "The maximum number of bodies the spider spawns in and decorates its nest with during its setup phase. Set both min and max to 0 to disable. Keep in mind that this value should be higher than Min Web Count, excessively high values may result in lag, Min should be less than Max.");
            MinWebCount = cfg.BindSyncedEntry("Webs",
                        "Min Web Count",
                        20,
                        "The minimum amount of webs a given spider can place, Min should be less than Max.");
            MaxWebCount = cfg.BindSyncedEntry("Webs",
                        "Max Web Count",
                        25,
                        "The maximum amount of webs a given spider can place, Min should be less than Max.");
            MinWebLength = cfg.BindSyncedEntry("Webs",
                        "Min Web Length",
                        2f,
                        "The minimum length a web can be in units, !! Keep in mind that the smaller the difference between the min and max length, the less spots a spider could successfully place a web!!");
            MaxWebLength = cfg.BindSyncedEntry("Webs",
                        "Max Web Length",
                        10f,
                        "The maximum length a web can be in units, !! Keep in mind that the smaller the differnce between the min and max length, the less spots a spider could successfully place a web!!");
            MinWebDistance = cfg.BindSyncedEntry("Webs",
                        "Min Distance Between Webs",
                        0.5f,
                        "This controls the distance in units a spider must be from the closest web in order to place a new one, a lower value will make webs closely knit, a higher value will make webs spaced further apart.");
            WebPlaceInterval = cfg.BindSyncedEntry("Webs",
                       "Web Placement Interval",
                       2f,
                       "How long in seconds the spider should wait after placing a web to place another");
            WebTimeRange = cfg.BindSyncedEntry("Webs",
                        "Web Placement Interval Variation",
                        0.5f,
                        "This increases the interval and gives it some variation, the Web Placement Interval plus this value will be the max interval, the min interval will be the Web Placement Interval plus half of this value. (2 & 0.5 for these settings will result in a 2.25-2.5 second interval, 10 & 7 will result in a 13.5-17 second interval. etc)");
            FailedWebTrapTime = cfg.BindSyncedEntry("Webs",
                        "Failed Web Placement Cooldown",
                        0.1f,
                        "How long in seconds spider should wait after failing to place a web before attempting to place another.");
        }
    }
}