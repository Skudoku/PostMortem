using System.Collections.Generic;
using DeathRecap;
using Xunit;

namespace DeathRecap.Tests
{
    public class TipEngineTests
    {
        // A death with nothing notable about it: fed, rested, healthy, full stamina, killed by
        // something ordinary in one hit. Individual tests turn on only what they're checking.
        private static DeathContext Ordinary() => new DeathContext
        {
            HitType = HitData.HitType.EnemyHit,
            MajorityType = HitData.DamageType.Slash,
            AttackerName = "Greyling",
            TotalDamage = 20f,
            HasVitals = true,
            MinStaminaPercent = 90f,
            RecentHealthFraction = 0.5f,
            HitsInLast3s = 1,
            FoodCount = 3,
            Rested = true
        };

        private static List<string> Keys(DeathContext ctx) => TipEngine.MatchingKeys(ctx);

        [Fact]
        public void OrdinaryDeath_FallsBackRatherThanInventingATip()
        {
            DeathContext ctx = Ordinary();
            // singlehit is legitimately specific enough to fire here; what matters is that no
            // preparation nagging shows up when the player did prepare.
            Assert.DoesNotContain("tip_nofood", Keys(ctx));
            Assert.DoesNotContain("tip_notrested", Keys(ctx));
        }

        // Regression: a slow poison tick from full health used to report itself as a one-shot,
        // because "was healthy recently" was true and DoT ticks aren't counted as combat hits.
        [Fact]
        public void PoisonTickFromFullHealth_IsNotReportedAsAOneShot()
        {
            DeathContext ctx = Ordinary();
            ctx.HitType = HitData.HitType.Poisoned;
            ctx.MajorityType = HitData.DamageType.Poison;
            ctx.Poison = true;
            ctx.RecentHealthFraction = 1.0f;
            ctx.HitsInLast3s = 0;

            Assert.DoesNotContain("tip_oneshot_healthy", Keys(ctx));
        }

        [Fact]
        public void RealOneShotFromFullHealth_IsReported()
        {
            DeathContext ctx = Ordinary();
            ctx.HitType = HitData.HitType.EnemyHit;
            ctx.RecentHealthFraction = 1.0f;
            ctx.HitsInLast3s = 1;
            ctx.AttackerName = "Deathsquito";

            Assert.Contains("tip_oneshot_healthy", Keys(ctx));
        }

        // Regression: generic preparation advice used to dilute the specific explanation, because
        // every matching rule went into one pool regardless of how much it explained.
        [Fact]
        public void SpecificCircumstanceBeatsGenericPreparationAdvice()
        {
            DeathContext ctx = Ordinary();
            ctx.FoodCount = 0;          // generic tier
            ctx.Rested = false;         // generic tier
            ctx.HitsInLast3s = 5;       // precise tier - got swarmed

            List<string> keys = Keys(ctx);
            Assert.Contains("tip_burst", keys);
            Assert.DoesNotContain("tip_nofood", keys);
            Assert.DoesNotContain("tip_notrested", keys);
        }

        [Fact]
        public void PreparationAdviceStillSurfacesWhenNothingSharperApplies()
        {
            DeathContext ctx = Ordinary();
            ctx.FoodCount = 0;
            ctx.HitsInLast3s = 1;
            ctx.RecentHealthFraction = 0.4f;
            ctx.AttackerName = null;      // no attacker, so no singlehit/oneshot rule
            ctx.TotalDamage = 0f;

            Assert.Contains("tip_nofood", Keys(ctx));
        }

        [Fact]
        public void WetAndFreezing_PrefersTheCombinationOverPlainCold()
        {
            DeathContext ctx = Ordinary();
            ctx.HitType = HitData.HitType.Freezing;
            ctx.Wet = true;
            ctx.Freezing = true;
            ctx.Cold = true;

            List<string> keys = Keys(ctx);
            Assert.Contains("tip_wet_freezing", keys);
            Assert.DoesNotContain("tip_cold", keys);
            Assert.DoesNotContain("tip_freezing", keys);
        }

        [Fact]
        public void TarAndFire_PrefersTheCombinationOverPlainTar()
        {
            DeathContext ctx = Ordinary();
            ctx.HitType = HitData.HitType.Burning;
            ctx.Burning = true;
            ctx.Tarred = true;

            List<string> keys = Keys(ctx);
            Assert.Contains("tip_burning_tarred", keys);
            Assert.DoesNotContain("tip_tarred", keys);
        }

        [Fact]
        public void KilledWhileHoldingAPickaxe_IsReportedAsAnAmbush()
        {
            DeathContext ctx = Ordinary();
            ctx.WieldingTool = true;
            ctx.WieldingHammer = false;

            Assert.Contains("tip_tool_ambush", Keys(ctx));
        }

        [Fact]
        public void BuildingDeaths_UseTheHammerVariantNotTheGenericOne()
        {
            DeathContext ctx = Ordinary();
            ctx.HitType = HitData.HitType.Fall;
            ctx.AttackerName = null;
            ctx.WieldingHammer = true;
            ctx.WieldingTool = true;

            List<string> keys = Keys(ctx);
            Assert.Contains("tip_fall_hammer", keys);
            Assert.DoesNotContain("tip_fall", keys);
        }

        [Fact]
        public void RepeatedDeathsToTheSameEnemy_EscalateToNemesis()
        {
            DeathContext ctx = Ordinary();
            ctx.AttackerName = "Troll";
            ctx.SameKillerAsLastDeath = true;
            ctx.TimesKilledByThisBefore = 3;

            List<string> keys = Keys(ctx);
            Assert.Contains("tip_nemesis", keys);
            Assert.DoesNotContain("tip_repeat_killer", keys);
        }

        [Fact]
        public void LowStaminaUsesPercentage_SoItMeansTheSameAtEveryStaminaPool()
        {
            DeathContext ctx = Ordinary();
            ctx.MinStaminaPercent = 5f;   // 5% - empty whether max is 50 or 300
            Assert.True(ctx.LowStamina);

            ctx.MinStaminaPercent = 50f;
            Assert.False(ctx.LowStamina);
        }

        [Fact]
        public void MissingVitalsData_NeverCountsAsLowStamina()
        {
            DeathContext ctx = Ordinary();
            ctx.HasVitals = false;
            ctx.MinStaminaPercent = -1f;

            Assert.False(ctx.LowStamina);
            Assert.DoesNotContain("tip_stamina_enemy", Keys(ctx));
        }

        [Fact]
        public void EveryRuleKeyIsUnique()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach ((int _, System.Func<DeathContext, bool> _, string key) in TipEngine.Rules)
            {
                Assert.True(seen.Add(key), $"Duplicate rule key: {key}");
            }
        }
    }
}
