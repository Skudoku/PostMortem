using System.Collections.Generic;

namespace DeathRecap
{
    internal static class RecapBuilder
    {
        internal static string Build(DeathContext ctx)
        {
            string line = BuildKillerLine(ctx);

            if (!Plugin.ShowTips.Value)
            {
                return line;
            }

            string tip = Loc.Tr(TipEngine.PickTipKey(ctx));
            return tip != null ? $"{line}\n<i>{tip}</i>" : line;
        }

        private static string BuildKillerLine(DeathContext ctx)
        {
            string typeName = DamageTypeLabel(ctx.MajorityType);
            string detail = ctx.TotalDamage > 0 ? $"{typeName}, {ctx.TotalDamage:F0} dmg" : typeName;

            string line;
            if (!string.IsNullOrEmpty(ctx.AttackerName))
            {
                line = Loc.Tr("line_killed_by", ctx.AttackerName, detail);
            }
            else
            {
                // Fall/Drowning/Burning/Freezing/Poisoned/Tree/etc. are already self-explanatory
                // HitType values. But EnemyHit/PlayerHit/etc. are generic - if we couldn't resolve
                // which mob it was (e.g. it despawned right after landing the killing blow), fall
                // back to whatever status effects were active instead of an uninformative label.
                string statusFlavor = IsGenericHitType(ctx.HitType) ? ActiveStatusEffectFlavor(ctx) : null;
                line = statusFlavor != null
                    ? Loc.Tr("line_died_to_while", HitTypeLabel(ctx.HitType), statusFlavor, detail)
                    : Loc.Tr("line_died_to", HitTypeLabel(ctx.HitType), detail);
            }

            if (Plugin.ShowBiome.Value && ctx.Biome != Heightmap.Biome.None)
            {
                line += " " + Loc.Tr("line_in_biome", BiomeLabel(ctx.Biome));
            }

            return line;
        }

        private static bool IsGenericHitType(HitData.HitType type)
        {
            return type == HitData.HitType.Undefined
                || type == HitData.HitType.EnemyHit
                || type == HitData.HitType.PlayerHit
                || type == HitData.HitType.Impact
                || type == HitData.HitType.Structural
                || type == HitData.HitType.Turret;
        }

        // Reuses Valheim's OWN localization entries for damage type names (already
        // professionally translated into every supported language) instead of maintaining
        // our own translations - same terms the player already sees in item tooltips.
        private static string DamageTypeLabel(HitData.DamageType type)
        {
            string key;
            switch (type)
            {
                // NOTE: HitData.DamageTypes.GetMajorityDamageType() defaults to DamageType.Damage
                // and only overrides it if a MORE SPECIFIC field (slash/pierce/fire/etc.) is
                // strictly greater - m_blunt is never itself compared. In practice this means
                // "Damage" is what a plain blunt hit reports as, not a rare fallback case - so it
                // needs a real label, not just an unlocalized "dmg_Damage" leaking through.
                case HitData.DamageType.Damage: key = "$inventory_damage"; break;
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

        // Biome names also exist in Valheim's own localization ($biome_meadows etc.).
        private static string BiomeLabel(Heightmap.Biome biome)
        {
            string key = "$biome_" + biome.ToString().ToLowerInvariant();
            if (Localization.instance != null)
            {
                string localized = Localization.instance.Localize(key);
                if (localized != key)
                {
                    return localized;
                }
            }
            return biome.ToString();
        }

        // Burning/Freezing/Poisoned reuse the same shared status-effect words used in
        // ActiveStatusEffectFlavor, so we don't keep two translations of the same word.
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
}
