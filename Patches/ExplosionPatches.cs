using HarmonyLib;
using UnityEngine;
using System;

namespace HapticsPlugin.Patches
{
    /// <summary>
    /// Fires haptics when an explosion goes off near the player.
    /// Intensity falls off with distance — close blast = full power.
    /// The distance-scaled value is then multiplied by the user's intensity slider.
    /// </summary>
    [HarmonyPatch(typeof(Explosion), "Explode")]
    public class ExplosionPatch
    {
        static void Postfix(Explosion __instance)
        {
            // Note: Explosion is not a MonoBehaviour in 7DTD 1.x, so .transform and .expData
            // are not available. Distance-scaled intensity is not possible without the position field
            // name. TODO: inspect Explosion via dnSpy to add distance gating.
            // For now: fire at full configured intensity whenever any explosion occurs.
            if (GameManager.Instance?.World?.GetPrimaryPlayer() == null) return;
            HapticsConfig.Explosion.Fire();
        }
    }

    /// <summary>
    /// Single patch covering both GunShot and BowShot (1e/7g fix).
    /// Both use ItemActionRanged.FireShot, so a single Postfix fires one string
    /// comparison per shot instead of two redundant ones in separate patches.
    ///
    /// ActionsHolder removed in 7DTD 1.x — use primary player singleton instead.
    /// ItemClass.GetItemType() removed in 7DTD 1.x — use holdingItem.Name instead.
    /// Crossbow excluded from bow detection (trigger mechanism like a gun).
    /// </summary>
    [HarmonyPatch(typeof(ItemActionRanged), "FireShot")]
    public class RangedFirePatch
    {
        static void Postfix(ItemActionRanged __instance)
        {
            // ActionsHolder removed in 7DTD 1.x — use primary player singleton.
            var player = GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
            if (player == null) return;
            if (PatchHelpers.IsSpectator(player)) return;

            // Use item Name (e.g. "gunBow", "gunCrossbow") — GetItemType() removed in 1.x.
            // string.Contains(string, StringComparison) not on .NET 4.8 — use IndexOf.
            string itemName = player.inventory?.holdingItem?.Name ?? "";
            bool isBow = itemName.IndexOf("Bow",      StringComparison.OrdinalIgnoreCase) >= 0
                      && itemName.IndexOf("Crossbow",  StringComparison.OrdinalIgnoreCase) < 0;

            if (isBow)
                HapticsConfig.BowShot.Fire();
            else
                HapticsConfig.GunShot.Fire();
        }
    }
}
