namespace DeathRecap
{
    // Pure data describing one death. Holds no game references beyond the enums it reports, so
    // the rule engine that reads it can be exercised without the game running.
    internal class DeathContext
    {
        public HitData.DamageType MajorityType;
        public HitData.HitType HitType;
        public string AttackerName;
        public bool AttackerWasBoss;
        public float TotalDamage;
        public Heightmap.Biome Biome;

        public float MinStaminaPercent;    // -1 when unknown
        public bool HasVitals;
        public float RecentHealthFraction; // health fraction shortly before death, -1 when unknown
        public int HitsInLast3s;

        public int FoodCount;
        public bool Rested;

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
        public bool WieldingTool;      // any non-combat tool: hammer, pickaxe, hoe, cultivator

        public int TimesKilledByThisBefore;
        public bool SameKillerAsLastDeath;

        // Died to something that actually swung at you, as opposed to fall damage, drowning or
        // a damage-over-time effect ticking you down.
        public bool IsCombatHit => HitType == HitData.HitType.EnemyHit || HitType == HitData.HitType.PlayerHit;

        public bool LowStamina => HasVitals && MinStaminaPercent >= 0
            && MinStaminaPercent <= Tuning.LowStaminaPercent;

        // "Went from healthy to dead." Deliberately narrow: a short lookback plus a real combat
        // hit, so a slow poison or burn tick from full health can't masquerade as a one-shot.
        public bool DiedFromHealthy => IsCombatHit && RecentHealthFraction >= 0.9f;
    }
}
