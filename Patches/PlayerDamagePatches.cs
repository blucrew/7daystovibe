using HarmonyLib;
using System;

namespace HapticsPlugin.Patches
{
    /// <summary>
    /// Fires haptics when the local player takes damage.
    /// Intensity scales with damage amount — a light scratch vs a zombie mauling —
    /// then multiplied by the user's configured intensity slider.
    ///
    /// Note: DamageResponse.Damage renamed to DamageResponse.Strength in 7DTD 1.x.
    /// Note: DamageSource.DamageTypes removed in 7DTD 1.x; bite detection moved to
    ///       PlayerBitePatch which now triggers on buff application (buffInfected).
    /// </summary>
    [HarmonyPatch(typeof(EntityPlayer), nameof(EntityPlayer.DamageEntity))]
    public class PlayerDamagePatch
    {
        static void Postfix(EntityPlayer __instance, DamageResponse _dmResponse)
        {
            // isLocalPlayer removed in 7DTD 1.x — use type check instead.
            if (!(__instance is EntityPlayerLocal)) return;
            if (_dmResponse.Strength <= 0) return;

            // Scale 0.1–1.0 with damage (20 hp = full intensity).
            // Math.Clamp not available on .NET Framework 4.8 — use Max/Min.
            float scaled = (float)Math.Max(0.1, Math.Min(1.0, _dmResponse.Strength / 20.0));
            HapticsConfig.PlayerDamage.Fire(scaled);
        }
    }

    /// <summary>
    /// Pulse when the player heals (first aid kit, bandage, food, etc.).
    /// Note: this patch covers the legacy Heal() code path. Most healing in 7DTD 1.x
    /// goes through the health-delta detection in StatusTickPatch instead.
    /// </summary>
    [HarmonyPatch(typeof(EntityPlayer), "Heal")]
    public class PlayerHealPatch
    {
        static void Postfix(EntityPlayer __instance, float _health)
        {
            // isLocalPlayer removed in 7DTD 1.x — use type check instead.
            if (!(__instance is EntityPlayerLocal)) return;
            HapticsConfig.PlayerHeal.Fire();
        }
    }
}
