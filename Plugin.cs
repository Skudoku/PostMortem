using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DeathRecap
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "mod.deathrecap";
        public const string PluginName = "Death Recap";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static string PluginDir;

        internal static ConfigEntry<bool> ShowTips;
        internal static ConfigEntry<bool> ShowBiome;
        internal static ConfigEntry<bool> BroadcastToOthers;
        internal static ConfigEntry<bool> WriteDeathLog;
        internal static ConfigEntry<float> LowStaminaPercent;
        internal static ConfigEntry<int> BurstHitCount;

        private void Awake()
        {
            Log = Logger;
            PluginDir = Path.GetDirectoryName(Info.Location) ?? "";

            ShowTips = Config.Bind("General", "Show tips", true,
                "Show a situational tip under the death line.");
            ShowBiome = Config.Bind("General", "Show biome", true,
                "Include the biome you died in.");
            BroadcastToOthers = Config.Bind("Multiplayer", "Broadcast my deaths", false,
                "Send your death recap to other players running this mod, so they see what killed you.");
            WriteDeathLog = Config.Bind("General", "Write death log", false,
                "Append every death recap to DeathLog.txt next to the plugin.");
            LowStaminaPercent = Config.Bind("Tuning", "Low stamina percent", 10f,
                "Stamina at or below this percent of max counts as 'empty' for tips.");
            BurstHitCount = Config.Bind("Tuning", "Burst hit count", 3,
                "How many combat hits within 3 seconds counts as a 'flurry of hits'.");

            ApplyTuning();
            LowStaminaPercent.SettingChanged += (_, __) => ApplyTuning();
            BurstHitCount.SettingChanged += (_, __) => ApplyTuning();

            Loc.LoadAll(Path.Combine(PluginDir, "Lang"));
            Log.LogInfo($"Loaded {Loc.LoadedLanguageCount} language file(s).");

            DeathHistory.Load();

            new Harmony(PluginGUID).PatchAll();

            StartCoroutine(SampleVitalsLoop());
        }

        private static void ApplyTuning()
        {
            Tuning.LowStaminaPercent = LowStaminaPercent.Value;
            Tuning.BurstHitCount = BurstHitCount.Value;
        }

        private IEnumerator SampleVitalsLoop()
        {
            WaitForSeconds wait = new WaitForSeconds(0.25f);
            while (true)
            {
                yield return wait;

                Player player = Player.m_localPlayer;
                if (player != null)
                {
                    VitalsTracker.Record(Time.time, player.GetStamina(), player.GetHealth(), player.GetMaxHealth());
                }
            }
        }
    }
}
