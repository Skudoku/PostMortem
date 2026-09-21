using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
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

        private void Awake()
        {
            Log = Logger;

            string langDir = Path.Combine(Path.GetDirectoryName(Info.Location) ?? "", "Lang");
            Loc.LoadAll(langDir);
            Log.LogInfo($"[DeathRecap] Loaded {Loc.LoadedLanguageCount} language file(s) from '{langDir}'.");

            Harmony harmony = new Harmony(PluginGUID);
            harmony.PatchAll();

            StartCoroutine(SampleVitalsLoop());
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
                    VitalsTracker.Record(Time.time, player.GetStamina(), player.GetHealth());
                }
            }
        }
    }

    // Loose per-language "key=value" text files in a Lang/ folder next to the DLL, one file
    // per language named exactly as Valheim's own Localization.GetSelectedLanguage() returns
    // it (e.g. "English.txt", "German.txt"). Falls back to English for any missing key or
    // missing language file, so a partial translation never breaks anything.
    internal static class Loc
    {
        private const string DefaultLanguage = "English";
        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();

        internal static int LoadedLanguageCount => Tables.Count;

        internal static void LoadAll(string langDir)
        {
            Tables.Clear();
            if (!Directory.Exists(langDir))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(langDir, "*.txt"))
            {
                string language = Path.GetFileNameWithoutExtension(file);
                Dictionary<string, string> table = new Dictionary<string, string>();

                foreach (string rawLine in File.ReadAllLines(file))
                {
                    string line = rawLine.TrimEnd('\r');
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    {
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1);
                    table[key] = value;
                }

                Tables[language] = table;
            }
        }

        internal static string Tr(string key, params object[] args)
        {
            string language = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : DefaultLanguage;
            string raw = Lookup(language, key) ?? Lookup(DefaultLanguage, key) ?? key;
            return args != null && args.Length > 0 ? string.Format(raw, args) : raw;
        }

        private static string Lookup(string language, string key)
        {
            if (language != null && Tables.TryGetValue(language, out Dictionary<string, string> table) && table.TryGetValue(key, out string value))
            {
                return value;
            }
            return null;
        }
    }

    internal struct HitRecord
    {
        public float Time;
        public float Damage;
        public HitData.DamageType MajorityType;
        public HitData.HitType HitType;
    }

    internal struct VitalSample
    {
        public float Time;
        public float Stamina;
        public float Health;
    }

    internal static class HitTracker
    {
        private const float WindowSeconds = 12f;
        internal static readonly List<HitRecord> Hits = new List<HitRecord>();

        internal static void Record(HitRecord record)
        {
            Hits.Add(record);
            Hits.RemoveAll(h => record.Time - h.Time > WindowSeconds);
        }

        // Only counts actual combat hits (from an enemy or player) - excludes damage-over-time
        // types like Drowning/Poison/Burning/Freezing, which tick repeatedly on their own and
        // would otherwise get miscounted as a "flurry of hits" from an attacker.
        internal static int CountCombatHitsInLast(float seconds)
        {
            float now = Time.time;
            int count = 0;
            foreach (HitRecord h in Hits)
            {
                if (now - h.Time <= seconds
                    && (h.HitType == HitData.HitType.EnemyHit || h.HitType == HitData.HitType.PlayerHit))
                {
                    count++;
                }
            }
            return count;
        }
    }

    internal static class VitalsTracker
    {
        private const float WindowSeconds = 12f;
        internal static readonly List<VitalSample> Samples = new List<VitalSample>();

        internal static void Record(float time, float stamina, float health)
        {
            Samples.Add(new VitalSample { Time = time, Stamina = stamina, Health = health });
            Samples.RemoveAll(s => time - s.Time > WindowSeconds);
        }

        internal static float MinStaminaInLast(float seconds)
        {
            float now = Time.time;
            float min = float.MaxValue;
            bool any = false;
            foreach (VitalSample s in Samples)
            {
                if (now - s.Time <= seconds)
                {
                    min = Mathf.Min(min, s.Stamina);
                    any = true;
                }
            }
            return any ? min : -1f;
        }
    }

    [HarmonyPatch(typeof(Character), "Damage")]
    internal static class Character_Damage_TrackHits
    {
        private static void Postfix(Character __instance, HitData hit)
        {
            Player localPlayer = Player.m_localPlayer;
            if (localPlayer == null || __instance != localPlayer || hit == null)
            {
                return;
            }

            HitTracker.Record(new HitRecord
            {
                Time = Time.time,
                Damage = hit.m_damage.GetTotalDamage(),
                MajorityType = hit.m_damage.GetMajorityDamageType(),
                HitType = hit.m_hitType
            });
        }
    }

    internal class DeathContext
    {
        public HitData.DamageType MajorityType;
        public HitData.HitType HitType;
        public string AttackerName;
        public float TotalDamage;
        public float MinStaminaLast4s;
        public int HitsInLast3s;
        public bool Wet;
        public bool Freezing;
        public bool Cold;
        public bool Burning;
        public bool Poison;
        public bool Frost;
        public bool Lightning;
        public bool Spirit;
        public bool Smoked;
        public bool Tarred;
        public bool Encumbered;
        public bool WieldingHammer;
    }

    [HarmonyPatch(typeof(Player), "OnDeath")]
    internal static class Player_OnDeath_ShowRecap
    {
        private static void Postfix(Player __instance, HitData ___m_lastHit)
        {
            if (__instance != Player.m_localPlayer)
            {
                return;
            }

            DeathContext ctx = BuildContext(__instance, ___m_lastHit);
            string recap = RecapBuilder.Build(ctx);
            __instance.Message(MessageHud.MessageType.Center, recap);
            Plugin.Log.LogInfo("[DeathRecap] " + recap.Replace("\n", " | "));
        }

        private static DeathContext BuildContext(Player player, HitData hit)
        {
            DeathContext ctx = new DeathContext
            {
                MinStaminaLast4s = VitalsTracker.MinStaminaInLast(4f),
                HitsInLast3s = HitTracker.CountCombatHitsInLast(3f)
            };

            if (hit != null)
            {
                ctx.MajorityType = hit.m_damage.GetMajorityDamageType();
                ctx.HitType = hit.m_hitType;
                ctx.TotalDamage = hit.m_damage.GetTotalDamage();
                ctx.AttackerName = ResolveAttackerName(hit);
            }

            SEMan seman = player.GetSEMan();
            if (seman != null)
            {
                ctx.Wet = seman.HaveStatusEffect(SEMan.s_statusEffectWet);
                ctx.Freezing = seman.HaveStatusEffect(SEMan.s_statusEffectFreezing);
                ctx.Cold = seman.HaveStatusEffect(SEMan.s_statusEffectCold);
                ctx.Burning = seman.HaveStatusEffect(SEMan.s_statusEffectBurning);
                ctx.Poison = seman.HaveStatusEffect(SEMan.s_statusEffectPoison);
                ctx.Frost = seman.HaveStatusEffect(SEMan.s_statusEffectFrost);
                ctx.Lightning = seman.HaveStatusEffect(SEMan.s_statusEffectLightning);
                ctx.Spirit = seman.HaveStatusEffect(SEMan.s_statusEffectSpirit);
                ctx.Smoked = seman.HaveStatusEffect(SEMan.s_statusEffectSmoked);
                ctx.Tarred = seman.HaveStatusEffect(SEMan.s_statusEffectTared);
                ctx.Encumbered = seman.HaveStatusEffect(SEMan.s_statusEffectEncumbered);
            }

            ctx.WieldingHammer = IsHammer(GetRightItem(player)) || IsHammer(GetLeftItem(player));

            return ctx;
        }

        // Humanoid.GetRightItem()/GetLeftItem() are protected - reach the backing fields
        // directly via reflection instead.
        private static readonly System.Reflection.FieldInfo RightItemField = AccessTools.Field(typeof(Humanoid), "m_rightItem");
        private static readonly System.Reflection.FieldInfo LeftItemField = AccessTools.Field(typeof(Humanoid), "m_leftItem");

        private static ItemDrop.ItemData GetRightItem(Player player) => (ItemDrop.ItemData)RightItemField.GetValue(player);
        private static ItemDrop.ItemData GetLeftItem(Player player) => (ItemDrop.ItemData)LeftItemField.GetValue(player);

        private static bool IsHammer(ItemDrop.ItemData item)
        {
            return item != null && item.m_dropPrefab != null
                && item.m_dropPrefab.name.IndexOf("Hammer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveAttackerName(HitData hit)
        {
            if (hit.m_attacker == ZDOID.None || ZNetScene.instance == null)
            {
                return null;
            }

            GameObject go = ZNetScene.instance.FindInstance(hit.m_attacker);
            if (go == null)
            {
                return null;
            }

            Character character = go.GetComponent<Character>();
            if (character != null && !string.IsNullOrEmpty(character.m_name))
            {
                return Localization.instance != null
                    ? Localization.instance.Localize(character.m_name)
                    : character.m_name;
            }

            string name = go.name;
            int cloneIndex = name.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            return cloneIndex > 0 ? name.Substring(0, cloneIndex) : name;
        }
    }

    internal static class RecapBuilder
    {
        internal static string Build(DeathContext ctx)
        {
            string killerLine = BuildKillerLine(ctx);
            string tip = TipEngine.PickTip(ctx);
            return tip != null ? $"{killerLine}\n<i>{tip}</i>" : killerLine;
        }

        private static string BuildKillerLine(DeathContext ctx)
        {
            string typeName = DamageTypeLabel(ctx.MajorityType);
            string damageSuffix = ctx.TotalDamage > 0 ? $"{typeName}, {ctx.TotalDamage:F0} dmg" : typeName;

            if (!string.IsNullOrEmpty(ctx.AttackerName))
            {
                return Loc.Tr("line_killed_by", ctx.AttackerName, damageSuffix);
            }

            // Fall/Drowning/Burning/Freezing/Poisoned/Tree/etc. are already self-explanatory
            // HitType values. But EnemyHit/PlayerHit/etc. are generic - if we couldn't resolve
            // which mob it was (e.g. it despawned right after landing the killing blow), fall
            // back to whatever status effects were active instead of just an uninformative label.
            bool isGenericHitType = ctx.HitType == HitData.HitType.Undefined
                || ctx.HitType == HitData.HitType.EnemyHit
                || ctx.HitType == HitData.HitType.PlayerHit
                || ctx.HitType == HitData.HitType.Impact
                || ctx.HitType == HitData.HitType.Structural
                || ctx.HitType == HitData.HitType.Turret;

            if (isGenericHitType)
            {
                string statusFlavor = ActiveStatusEffectFlavor(ctx);
                if (statusFlavor != null)
                {
                    return Loc.Tr("line_died_to_while", HitTypeLabel(ctx.HitType), statusFlavor, damageSuffix);
                }
            }

            return Loc.Tr("line_died_to", HitTypeLabel(ctx.HitType), damageSuffix);
        }

        // Reuses Valheim's OWN localization entries for damage type names (already
        // professionally translated into every supported language) instead of maintaining
        // our own translations for these - same terms the player already sees in tooltips.
        private static string DamageTypeLabel(HitData.DamageType type)
        {
            string key;
            switch (type)
            {
                case HitData.DamageType.Blunt: key = "$inventory_blunt"; break;
                case HitData.DamageType.Slash: key = "$inventory_slash"; break;
                case HitData.DamageType.Pierce: key = "$inventory_pierce"; break;
                case HitData.DamageType.Fire: key = "$inventory_fire"; break;
                case HitData.DamageType.Frost: key = "$inventory_frost"; break;
                case HitData.DamageType.Lightning: key = "$inventory_lightning"; break;
                case HitData.DamageType.Poison: key = "$inventory_poison"; break;
                case HitData.DamageType.Spirit: key = "$inventory_spirit"; break;
                case HitData.DamageType.Chop: key = "$inventory_chop"; break;
                case HitData.DamageType.Pickaxe: key = "$inventory_pickaxe"; break;
                default: key = null; break;
            }

            if (key != null && Localization.instance != null)
            {
                string localized = Localization.instance.Localize(key);
                if (localized != key)
                {
                    return localized;
                }
            }

            return Loc.Tr("dmg_" + type);
        }

        // Fall/Drowning/EdgeOfWorld/Tree/etc. get our own translated word. Burning/Freezing/
        // Poisoned reuse the same shared status-effect words used in ActiveStatusEffectFlavor
        // below, so we don't maintain two separate translations of the same English word.
        private static string HitTypeLabel(HitData.HitType type)
        {
            switch (type)
            {
                case HitData.HitType.Burning: return Loc.Tr("Burning");
                case HitData.HitType.Freezing: return Loc.Tr("Freezing");
                case HitData.HitType.Poisoned: return Loc.Tr("Poisoned");
                default: return Loc.Tr("ht_" + type);
            }
        }

        private static string ActiveStatusEffectFlavor(DeathContext ctx)
        {
            List<string> active = new List<string>();
            if (ctx.Burning) active.Add(Loc.Tr("Burning"));
            if (ctx.Freezing) active.Add(Loc.Tr("Freezing"));
            else if (ctx.Cold) active.Add(Loc.Tr("Cold"));
            if (ctx.Poison) active.Add(Loc.Tr("Poisoned"));
            if (ctx.Wet) active.Add(Loc.Tr("Wet"));
            if (ctx.Tarred) active.Add(Loc.Tr("Tarred"));
            if (ctx.Smoked) active.Add(Loc.Tr("Smoked"));
            if (ctx.Encumbered) active.Add(Loc.Tr("Encumbered"));

            return active.Count > 0 ? string.Join(", ", active) : null;
        }
    }

    internal static class TipEngine
    {
        private static readonly List<(Func<DeathContext, bool> Condition, string Key)> Rules = new List<(Func<DeathContext, bool>, string)>
        {
            // Stamina
            (ctx => ctx.MinStaminaLast4s >= 0 && ctx.MinStaminaLast4s < 5 && ctx.HitType == HitData.HitType.EnemyHit, "tip_stamina_enemy"),
            (ctx => ctx.MinStaminaLast4s >= 0 && ctx.MinStaminaLast4s < 15 && ctx.Encumbered, "tip_stamina_encumbered"),
            (ctx => ctx.MinStaminaLast4s >= 0 && ctx.MinStaminaLast4s < 5 && ctx.HitType == HitData.HitType.Drowning, "tip_stamina_drowning"),

            // Status effect combos
            (ctx => ctx.Wet && ctx.Freezing, "tip_wet_freezing"),
            (ctx => ctx.Cold && !ctx.Wet, "tip_cold"),
            (ctx => ctx.Burning && ctx.Tarred, "tip_burning_tarred"),
            (ctx => ctx.Tarred && !ctx.Burning, "tip_tarred"),
            (ctx => ctx.Poison && ctx.MinStaminaLast4s >= 0 && ctx.MinStaminaLast4s < 15, "tip_poison_lowstamina"),
            (ctx => ctx.Poison && ctx.MajorityType == HitData.DamageType.Poison, "tip_poison_majority"),
            (ctx => ctx.Encumbered && ctx.HitType == HitData.HitType.Drowning, "tip_encumbered_drowning"),
            (ctx => ctx.Smoked, "tip_smoked"),
            (ctx => ctx.Lightning || ctx.MajorityType == HitData.DamageType.Lightning, "tip_lightning"),
            (ctx => ctx.Frost && ctx.MajorityType == HitData.DamageType.Frost, "tip_frost"),
            (ctx => ctx.MajorityType == HitData.DamageType.Spirit, "tip_spirit"),

            // Burst / combat pattern
            (ctx => ctx.HitsInLast3s >= 3, "tip_burst"),
            (ctx => ctx.HitsInLast3s <= 1 && ctx.TotalDamage > 0 && !string.IsNullOrEmpty(ctx.AttackerName), "tip_singlehit"),

            // Hit type flavor
            (ctx => ctx.HitType == HitData.HitType.Fall && ctx.WieldingHammer, "tip_fall_hammer"),
            (ctx => ctx.HitType == HitData.HitType.Fall && !ctx.WieldingHammer, "tip_fall"),
            (ctx => ctx.HitType == HitData.HitType.Drowning && ctx.WieldingHammer, "tip_drowning_hammer"),
            (ctx => ctx.HitType == HitData.HitType.Drowning && !ctx.WieldingHammer && !ctx.Encumbered && ctx.MinStaminaLast4s < 0, "tip_drowning"),
            (ctx => ctx.HitType == HitData.HitType.Burning && ctx.WieldingHammer && !ctx.Tarred, "tip_burning_hammer"),
            (ctx => ctx.HitType == HitData.HitType.Burning && !ctx.WieldingHammer && !ctx.Tarred, "tip_burning"),
            (ctx => ctx.HitType == HitData.HitType.Freezing, "tip_freezing"),
            (ctx => ctx.HitType == HitData.HitType.EdgeOfWorld, "tip_edgeofworld"),
            (ctx => ctx.HitType == HitData.HitType.Tree, "tip_tree"),
        };

        private static readonly List<string> FallbackKeys = new List<string>
        {
            "fallback_1", "fallback_2", "fallback_3", "fallback_4",
        };

        internal static string PickTip(DeathContext ctx)
        {
            List<string> matches = new List<string>();
            foreach ((Func<DeathContext, bool> condition, string key) in Rules)
            {
                if (condition(ctx))
                {
                    matches.Add(key);
                }
            }

            string chosenKey = matches.Count > 0
                ? matches[UnityEngine.Random.Range(0, matches.Count)]
                : FallbackKeys[UnityEngine.Random.Range(0, FallbackKeys.Count)];

            return Loc.Tr(chosenKey);
        }
    }
}
