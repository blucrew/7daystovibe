using HarmonyLib;

namespace HapticsPlugin.Patches
{
    /// <summary>
    /// Shared utilities used across multiple Harmony patches.
    /// </summary>
    internal static class PatchHelpers
    {
        /// <summary>
        /// Returns true if the player is currently in spectator / observer mode
        /// (e.g. after death, before respawn).
        ///
        /// Uses Traverse reflection to access the private "isSpectator" field.
        /// If the field does not exist in this game version, returns false (safe — no false positives).
        ///
        /// Pattern adopted from both bHaptics and OWO reference mods where every
        /// patch checks this before firing to prevent haptics during spectate.
        /// </summary>
        internal static bool IsSpectator(EntityPlayerLocal player)
        {
            return Traverse.Create(player).Field("isSpectator").GetValue<bool>();
        }
    }
}
