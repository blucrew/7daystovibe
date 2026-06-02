using HarmonyLib;
using UnityEngine;

namespace HapticsPlugin.Patches
{
    // ── Session reset (A-15) ─────────────────────────────────────────────────
    // Resets per-frame timestamp static fields when a new game world loads,
    // preventing the "large timestamp blocks first ticks" bug.

    [HarmonyPatch(typeof(GameManager), "StartGame")]
    public class SessionResetPatch
    {
        static void Postfix()
        {
            // Reset all per-frame static timestamps so they don't block events
            // after a world reload (Time.timeAsDouble resets to 0 on new world).
            StatusTickPatch.ResetTimestamps();
            VehicleSpeedPatch.ResetTimestamp();  // 6a fix
        }
    }

    // ── Traps ─────────────────────────────────────────────────────────────────
    // NOTE (1.x): BlockLandMine no longer exists. In 7DTD 1.x, landmines use PowerPressurePlate
    // (the same power item as pressure plate traps). We hook HandleSingleUseDisable which fires
    // when a single-use pressure plate is consumed — the exact moment a landmine triggers.
    // PowerPressurePlate doesn't carry an entity reference at this point, so we check whether
    // the local player is within a tight radius of the triggered tile entity position instead.

    [HarmonyPatch(typeof(PowerPressurePlate), "HandleSingleUseDisable")]
    public class LandminePatch
    {
        static void Postfix(PowerPressurePlate __instance)
        {
            // Only fire if the local player is very close (within 3 blocks)
            var player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null) return;

            var te = __instance.TileEntity;
            if (te == null) { HapticsConfig.Landmine.Fire(); return; }  // fallback: fire anyway

            float dist = Vector3.Distance(player.position, te.ToWorldPos().ToVector3());
            if (dist <= 3f) HapticsConfig.Landmine.Fire();
        }
    }

    // NOTE (1.x): BlockElectricWireRelay.OnEntityTouched no longer exists.
    // PowerElectricWireRelay (the 1.x rename) has no OnEntityTouched method.
    // Electric shock is now detected via EntityBuffs.AddBuff — see AlarmTriggeredPatch below,
    // which has been extended to also catch buffElectricShock.

    // NOTE (A-9): BlockSpikes is the decorative floor spike, NOT the spinning blade trap.
    // The spinning blade trap entity is typically "EntityEnemyAnimal" or has its own
    // TrapBlade block class. Swap the patch target once confirmed via FindMethods.csx.
    // Left as BlockSpikes for now — it will still fire when stepping on spike traps.

    [HarmonyPatch(typeof(BlockSpikes), "OnEntityWalksOnBlock")]
    public class BladeTrapPatch
    {
        static void Postfix(Entity _entity)
        {
            if (!(_entity is EntityPlayerLocal)) return;
            HapticsConfig.BladeTrap.Fire();
        }
    }

    // ── Air Drop (A-10 fix: distance gate) ───────────────────────────────────

    // NOTE (1.x): AirDropManager no longer exists. The 1.x equivalent is
    // AIDirectorAirDropComponent.SpawnAirDrop which fires when an airdrop is scheduled.
    // Distance gating removed for now — SpawnAirDrop doesn't carry the drop position yet.
    // TODO: add distance gate once the position field name is confirmed via dnSpy.

    [HarmonyPatch(typeof(AIDirectorAirDropComponent), "SpawnAirDrop")]
    public class AirDropPatch
    {
        static void Postfix()
        {
            HapticsConfig.AirDrop.Fire();
        }
    }

    // BloodMoonEndPatch moved to BloodMoonPatches.cs where BloodMoonStartPatch lives (7h fix).

    // ── Block Broken ─────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(EntityPlayer), "onBlockDestroyed")]
    public class BlockBrokenPatch
    {
        static void Postfix(EntityPlayer __instance)
        {
            // isLocalPlayer removed in 7DTD 1.x — use type check instead.
            if (!(__instance is EntityPlayerLocal)) return;
            HapticsConfig.BlockBroken.Fire();
        }
    }

    // ── Alarm (A-11 fix) ─────────────────────────────────────────────────────
    // BlockAlarmClock.OnBlockActivated fires when a player *sets* the alarm clock,
    // not when an alarm siren is triggered. Replaced with a buff-based approach:
    // the siren/motion sensor damage goes through a buff named "buffElectricShock"
    // or similar. Verify the exact buff name in-game and update accordingly.

    [HarmonyPatch(typeof(EntityBuffs), "AddBuff")]
    public class AlarmTriggeredPatch
    {
        static void Postfix(EntityBuffs __instance, string _buffName)
        {
            if (!(__instance.parent is EntityPlayerLocal)) return;

            // Alarm / siren trigger
            // TODO: verify exact buff name — common candidates: "buffAlarm", "buffSiren"
            if (_buffName.StartsWith("buffAlarm") || _buffName.StartsWith("buffSiren"))
            {
                HapticsConfig.AlarmTriggered.Fire();
                return;
            }

            // Electric trap (1.x replacement for BlockElectricWireRelay.OnEntityTouched)
            // TODO: verify exact buff name — common candidates: "buffElectricShock", "buffElectric"
            if (_buffName.StartsWith("buffElectric"))
            {
                HapticsConfig.ElectricTrap.Fire();
            }
        }
    }
}
