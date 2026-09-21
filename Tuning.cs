namespace DeathRecap
{
    // Plain values rather than reading BepInEx ConfigEntry directly, so the rule engine has no
    // dependency on the plugin host and can be exercised in tests. Plugin.Awake copies the
    // config into these on load and whenever the config changes.
    internal static class Tuning
    {
        internal static float LowStaminaPercent = 10f;
        internal static int BurstHitCount = 3;
    }
}
