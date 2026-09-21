using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DeathRecap
{
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
        public float MaxHealth;
    }

    internal static class HitTracker
    {
        private const float WindowSeconds = 12f;
        private static readonly List<HitRecord> Hits = new List<HitRecord>();

        internal static void Record(HitRecord record)
        {
            Hits.Add(record);
            Hits.RemoveAll(h => record.Time - h.Time > WindowSeconds);
        }

        // Cleared on death so a fast respawn-and-die-again can't count hits from the previous life.
        internal static void Clear() => Hits.Clear();

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
        private static readonly List<VitalSample> Samples = new List<VitalSample>();

        internal static void Record(float time, float stamina, float health, float maxHealth)
        {
            Samples.Add(new VitalSample { Time = time, Stamina = stamina, Health = health, MaxHealth = maxHealth });
            Samples.RemoveAll(s => time - s.Time > WindowSeconds);
        }

        internal static void Clear() => Samples.Clear();

        internal static bool HasSamples => Samples.Count > 0;

        // Lowest stamina seen in the window, as a percentage of max stamina. Percentage rather
        // than an absolute value so the threshold means the same thing for a fresh character
        // and a fully-upgraded one. Returns -1 when there is no data to judge from.
        internal static float MinStaminaPercentInLast(float seconds, float maxStamina)
        {
            if (maxStamina <= 0f)
            {
                return -1f;
            }

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

            return any ? (min / maxStamina) * 100f : -1f;
        }

        // Highest health fraction seen in the window - used to tell "went from full health to
        // dead" apart from "was already badly hurt and got finished off".
        internal static float MaxHealthFractionInLast(float seconds)
        {
            float now = Time.time;
            float max = -1f;
            foreach (VitalSample s in Samples)
            {
                if (now - s.Time <= seconds && s.MaxHealth > 0f)
                {
                    max = Mathf.Max(max, s.Health / s.MaxHealth);
                }
            }
            return max;
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
}
