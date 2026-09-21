using System;
using System.Collections.Generic;

namespace DeathRecap
{
    internal static class TipEngine
    {
        // How specific a rule is. Every matching rule is collected, but only the most specific
        // tier present is drawn from - otherwise every rule added to the list makes the good
        // ones rarer, and "you had no food" drowns out "you got swarmed by five Fulings".
        internal static class Tier
        {
            internal const int Generic = 1;   // standing advice that's true of many deaths
            internal const int Specific = 2;  // one clear circumstance
            internal const int Precise = 3;   // a combination that explains this particular death
        }

        internal static readonly List<(int Tier, Func<DeathContext, bool> Condition, string Key)> Rules =
            new List<(int, Func<DeathContext, bool>, string)>
        {
            // Preparation - true often enough that it should only surface when nothing sharper did
            (Tier.Generic, ctx => ctx.FoodCount == 0, "tip_nofood"),
            (Tier.Generic, ctx => ctx.FoodCount == 1, "tip_lowfood"),
            (Tier.Generic, ctx => !ctx.Rested && ctx.IsCombatHit, "tip_notrested"),

            // Recent history - knowing this thing keeps killing you beats any generic advice
            (Tier.Precise, ctx => ctx.TimesKilledByThisBefore >= 2, "tip_nemesis"),
            (Tier.Precise, ctx => ctx.SameKillerAsLastDeath && ctx.TimesKilledByThisBefore < 2, "tip_repeat_killer"),

            // Stamina
            (Tier.Precise, ctx => ctx.LowStamina && ctx.IsCombatHit, "tip_stamina_enemy"),
            (Tier.Precise, ctx => ctx.LowStamina && ctx.Encumbered, "tip_stamina_encumbered"),
            (Tier.Precise, ctx => ctx.LowStamina && ctx.HitType == HitData.HitType.Drowning, "tip_stamina_drowning"),

            // Status effect combinations
            (Tier.Precise, ctx => ctx.Wet && ctx.Freezing, "tip_wet_freezing"),
            (Tier.Specific, ctx => ctx.Cold && !ctx.Wet, "tip_cold"),
            (Tier.Precise, ctx => ctx.Burning && ctx.Tarred, "tip_burning_tarred"),
            (Tier.Specific, ctx => ctx.Tarred && !ctx.Burning, "tip_tarred"),
            (Tier.Precise, ctx => ctx.Poison && ctx.LowStamina, "tip_poison_lowstamina"),
            (Tier.Specific, ctx => ctx.Poison && ctx.MajorityType == HitData.DamageType.Poison, "tip_poison_majority"),
            (Tier.Precise, ctx => ctx.Encumbered && ctx.HitType == HitData.HitType.Drowning, "tip_encumbered_drowning"),
            (Tier.Specific, ctx => ctx.Smoked, "tip_smoked"),
            (Tier.Specific, ctx => ctx.Lightning || ctx.MajorityType == HitData.DamageType.Lightning, "tip_lightning"),
            (Tier.Specific, ctx => ctx.Frost && ctx.MajorityType == HitData.DamageType.Frost, "tip_frost"),
            (Tier.Specific, ctx => ctx.MajorityType == HitData.DamageType.Spirit, "tip_spirit"),

            // Combat pattern
            (Tier.Precise, ctx => ctx.HitsInLast3s >= Tuning.BurstHitCount, "tip_burst"),
            (Tier.Specific, ctx => ctx.HitsInLast3s <= 1 && ctx.TotalDamage > 0 && ctx.IsCombatHit
                    && !string.IsNullOrEmpty(ctx.AttackerName) && !ctx.DiedFromHealthy, "tip_singlehit"),
            (Tier.Precise, ctx => ctx.HitsInLast3s <= 1 && ctx.DiedFromHealthy
                    && !string.IsNullOrEmpty(ctx.AttackerName), "tip_oneshot_healthy"),
            (Tier.Precise, ctx => ctx.AttackerWasBoss, "tip_boss"),

            // Caught with a tool in hand rather than a weapon
            (Tier.Precise, ctx => ctx.WieldingTool && !ctx.WieldingHammer && ctx.IsCombatHit, "tip_tool_ambush"),

            // Environment
            (Tier.Precise, ctx => ctx.HitType == HitData.HitType.Fall && ctx.WieldingHammer, "tip_fall_hammer"),
            (Tier.Specific, ctx => ctx.HitType == HitData.HitType.Fall && !ctx.WieldingHammer, "tip_fall"),
            (Tier.Precise, ctx => ctx.HitType == HitData.HitType.Drowning && ctx.WieldingHammer, "tip_drowning_hammer"),
            (Tier.Specific, ctx => ctx.HitType == HitData.HitType.Drowning && !ctx.WieldingHammer && !ctx.Encumbered, "tip_drowning"),
            (Tier.Precise, ctx => ctx.HitType == HitData.HitType.Burning && ctx.WieldingHammer && !ctx.Tarred, "tip_burning_hammer"),
            (Tier.Specific, ctx => ctx.HitType == HitData.HitType.Burning && !ctx.WieldingHammer && !ctx.Tarred, "tip_burning"),
            (Tier.Specific, ctx => ctx.HitType == HitData.HitType.Freezing, "tip_freezing"),
            (Tier.Specific, ctx => ctx.HitType == HitData.HitType.EdgeOfWorld, "tip_edgeofworld"),
            (Tier.Specific, ctx => ctx.HitType == HitData.HitType.Tree, "tip_tree"),
        };

        internal static readonly List<string> FallbackKeys = new List<string>
        {
            "fallback_1", "fallback_2", "fallback_3", "fallback_4",
        };

        private static string _lastShownKey;

        // Pure: returns only the keys from the most specific tier that matched. Separated from
        // the random pick so it can be unit tested.
        internal static List<string> MatchingKeys(DeathContext ctx)
        {
            int bestTier = 0;
            foreach ((int tier, Func<DeathContext, bool> condition, string _) in Rules)
            {
                if (tier > bestTier && condition(ctx))
                {
                    bestTier = tier;
                }
            }

            List<string> matches = new List<string>();
            if (bestTier == 0)
            {
                return matches;
            }

            foreach ((int tier, Func<DeathContext, bool> condition, string key) in Rules)
            {
                if (tier == bestTier && condition(ctx))
                {
                    matches.Add(key);
                }
            }
            return matches;
        }

        // Returns a localization key, not text - translation is the presentation layer's job,
        // which keeps this whole class free of game and localization dependencies.
        internal static string PickTipKey(DeathContext ctx)
        {
            List<string> candidates = MatchingKeys(ctx);
            if (candidates.Count == 0)
            {
                candidates = new List<string>(FallbackKeys);
            }

            // Avoid showing the same line twice running when there's an alternative.
            if (candidates.Count > 1 && _lastShownKey != null)
            {
                candidates.Remove(_lastShownKey);
            }

            string chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            _lastShownKey = chosen;
            return chosen;
        }
    }
}
