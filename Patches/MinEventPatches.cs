using HarmonyLib;
using System;

namespace HapticsPlugin.Patches
{
    /// <summary>
    /// Taps into the game's internal MinEvent bus (EntityAlive.FireEvent) so we can
    /// respond to player state transitions without polling every frame.
    ///
    /// Events handled here (all confirmed by bHaptics and OWO reference mods):
    ///   onSelfWaterSubmerge  — player enters water
    ///   onSelfWaterSurface   — player surfaces from water
    ///   onSelfJump           — player leaves the ground on a jump
    ///   onSelfPrimaryActionStart — player begins a primary action (used for bow draw)
    ///
    /// NOTE: FireEvent fires for every EntityAlive, not just the local player.
    ///       The cast to EntityPlayerLocal is the local-player filter.
    ///
    /// NOTE: MinEventTypes enum values are confirmed stable across Alpha-21/22
    ///       (used in two independent reference mods, the most recent updated May 2025).
    /// </summary>
    [HarmonyPatch(typeof(EntityAlive), "FireEvent")]
    public class MinEventPatch
    {
        static void Postfix(EntityAlive __instance, MinEventTypes _eEventType)
        {
            // Only handle the local player
            if (!(__instance is EntityPlayerLocal player)) return;
            if (PatchHelpers.IsSpectator(player)) return;

            switch (_eEventType)
            {
                case MinEventTypes.onSelfWaterSubmerge:
                    HapticsConfig.PlayerWaterEnter.Fire();
                    break;

                case MinEventTypes.onSelfWaterSurface:
                    HapticsConfig.PlayerWaterExit.Fire();
                    break;

                case MinEventTypes.onSelfJump:
                    HapticsConfig.PlayerJump.Fire();
                    break;

                case MinEventTypes.onSelfPrimaryActionStart:
                    // Bow draw: single haptic at the start of drawing.
                    // Crossbows excluded — they don't have draw tension.
                    // Release/shot is already handled by RangedFirePatch (ItemActionRanged.FireShot).
                    if (IsBowInHand(player) && !IsCrossbowInHand(player))
                        HapticsConfig.BowDraw.Fire();
                    break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // Use ItemClass.Name (e.g. "gunBow", "gunCrossbow") for item type detection.
        // GetItemType() was removed in 7DTD 1.x; Name is the stable identifier.
        // Contains(string, StringComparison) not available on .NET 4.8 — use IndexOf.
        private static bool IsBowInHand(EntityPlayerLocal player)
        {
            string name = player.inventory?.holdingItem?.Name ?? "";
            return name.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCrossbowInHand(EntityPlayerLocal player)
        {
            string name = player.inventory?.holdingItem?.Name ?? "";
            return name.IndexOf("Crossbow", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
