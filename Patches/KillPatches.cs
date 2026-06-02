using HarmonyLib;

namespace HapticsPlugin.Patches
{
    /// <summary>
    /// Fires haptics when the local player kills a zombie/entity.
    ///
    /// Approach: patch EntityAlive.DamageEntity (which receives a DamageResponse with
    /// Source.EntityId and Fatal flag) rather than EntityAlive.Kill (where DamageSource
    /// is no longer a public property in 7DTD 1.x).
    /// </summary>
    [HarmonyPatch(typeof(EntityAlive), "DamageEntity")]
    public class ZombieKillPatch
    {
        static void Postfix(EntityAlive __instance, DamageResponse _dmResponse)
        {
            if (__instance is EntityPlayer) return;     // player death handled separately
            if (!_dmResponse.Fatal) return;             // only lethal hits

            // Check the killing blow came from the local player.
            int localId = GameManager.Instance?.World?.GetPrimaryPlayerId() ?? -1;
            if (localId < 0) return;
            // DamageSource.EntityId is not a public property in 7DTD 1.x — use Traverse.
            int sourceId = _dmResponse.Source != null
                ? Traverse.Create(_dmResponse.Source).Field("entityId").GetValue<int>()
                : -1;
            if (sourceId != localId) return;

            HapticsConfig.ZombieKill.Fire();
        }
    }

    /// <summary>
    /// Fires haptics when the local player dies.
    /// Also sets the flag that enables respawn haptics (A-14 fix).
    /// </summary>
    [HarmonyPatch(typeof(EntityPlayer), "OnEntityDeath")]
    public class PlayerDeathPatch
    {
        static void Postfix(EntityPlayer __instance)
        {
            // isLocalPlayer removed in 7DTD 1.x — use type check instead.
            if (!(__instance is EntityPlayerLocal)) return;
            HapticsConfig.PlayerDeath.Fire();
            PlayerRespawnPatch.MarkDied();  // A-14: enable respawn haptic for next spawn
        }
    }
}
