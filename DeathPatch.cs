using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DeathRecap
{
    [HarmonyPatch(typeof(Player), "OnDeath")]
    internal static class Player_OnDeath_ShowRecap
    {
        private static readonly FieldInfo RightItemField = AccessTools.Field(typeof(Humanoid), "m_rightItem");
        private static readonly FieldInfo LeftItemField = AccessTools.Field(typeof(Humanoid), "m_leftItem");

        private static readonly string[] ToolPrefabs = { "Hammer", "Pickaxe", "Hoe", "Cultivator" };

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

            DeathLog.Append(recap);
            DeathHistory.Record(ctx);
            DeathBroadcast.Send(recap);

            // Start the next life with a clean slate.
            HitTracker.Clear();
            VitalsTracker.Clear();
        }

        private static DeathContext BuildContext(Player player, HitData hit)
        {
            ItemDrop.ItemData right = GetItem(RightItemField, player);
            ItemDrop.ItemData left = GetItem(LeftItemField, player);

            DeathContext ctx = new DeathContext
            {
                HitsInLast3s = HitTracker.CountCombatHitsInLast(3f),
                HasVitals = VitalsTracker.HasSamples,
                MinStaminaPercent = VitalsTracker.MinStaminaPercentInLast(4f, player.GetMaxStamina()),
                RecentHealthFraction = VitalsTracker.MaxHealthFractionInLast(1.5f),
                Biome = player.GetCurrentBiome(),
                FoodCount = player.GetFoods()?.Count ?? 0,
                WieldingHammer = IsPrefab(right, "Hammer") || IsPrefab(left, "Hammer"),
                WieldingTool = IsAnyPrefab(right, ToolPrefabs) || IsAnyPrefab(left, ToolPrefabs)
            };

            if (hit != null)
            {
                ctx.MajorityType = hit.m_damage.GetMajorityDamageType();
                ctx.HitType = hit.m_hitType;
                ctx.TotalDamage = hit.m_damage.GetTotalDamage();
                ResolveAttacker(hit, ctx);
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
                ctx.Rested = seman.HaveStatusEffect(SEMan.s_statusEffectRested);
            }

            if (!string.IsNullOrEmpty(ctx.AttackerName))
            {
                ctx.TimesKilledByThisBefore = DeathHistory.TimesKilledBy(ctx.AttackerName);
                ctx.SameKillerAsLastDeath = DeathHistory.LastKillerWas(ctx.AttackerName);
            }

            return ctx;
        }

        private static void ResolveAttacker(HitData hit, DeathContext ctx)
        {
            if (hit.m_attacker == ZDOID.None || ZNetScene.instance == null)
            {
                return;
            }

            GameObject go = ZNetScene.instance.FindInstance(hit.m_attacker);
            if (go == null)
            {
                return;
            }

            Character character = go.GetComponent<Character>();
            if (character != null)
            {
                ctx.AttackerWasBoss = character.IsBoss();

                if (!string.IsNullOrEmpty(character.m_name))
                {
                    ctx.AttackerName = Localization.instance != null
                        ? Localization.instance.Localize(character.m_name)
                        : character.m_name;
                    return;
                }
            }

            string name = go.name;
            int cloneIndex = name.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            ctx.AttackerName = cloneIndex > 0 ? name.Substring(0, cloneIndex) : name;
        }

        // Humanoid.GetRightItem()/GetLeftItem() are protected - reach the backing fields directly.
        private static ItemDrop.ItemData GetItem(FieldInfo field, Player player)
        {
            return field?.GetValue(player) as ItemDrop.ItemData;
        }

        private static bool IsPrefab(ItemDrop.ItemData item, string prefix)
        {
            return item?.m_dropPrefab != null
                && item.m_dropPrefab.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAnyPrefab(ItemDrop.ItemData item, string[] prefixes)
        {
            foreach (string prefix in prefixes)
            {
                if (IsPrefab(item, prefix))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
