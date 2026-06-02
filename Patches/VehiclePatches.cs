using HarmonyLib;
using UnityEngine;

namespace HapticsPlugin.Patches
{
    // ── Vehicle Events ────────────────────────────────────────────────────────

    /// <summary>Fires when the player's vehicle collides with something.</summary>
    [HarmonyPatch(typeof(EntityVehicle), "OnCollisionEnter")]
    public class VehicleCollisionPatch
    {
        static void Postfix(EntityVehicle __instance, Collision _collision)
        {
            if (!VehiclePatchHelper.IsLocalPlayerVehicle(__instance)) return;
            float impact = _collision.relativeVelocity.magnitude;
            if (impact < 2f) return; // ignore tiny bumps

            float scaled = Mathf.Clamp01(impact / 20f);
            HapticsConfig.VehicleCollision.Fire(scaled);
        }
    }

    /// <summary>Fires when the player's vehicle takes damage.</summary>
    [HarmonyPatch(typeof(EntityVehicle), "DamageEntity")]
    public class VehicleDamagePatch
    {
        static void Postfix(EntityVehicle __instance, DamageResponse _dmResponse)
        {
            if (!VehiclePatchHelper.IsLocalPlayerVehicle(__instance)) return;
            // DamageResponse.Damage renamed to .Strength in 7DTD 1.x.
            if (_dmResponse.Strength <= 0) return;
            HapticsConfig.VehicleDamage.Fire();
        }
    }

    /// <summary>Fires when the player's vehicle is destroyed.</summary>
    [HarmonyPatch(typeof(EntityVehicle), "Kill")]
    public class VehicleDestroyedPatch
    {
        static void Postfix(EntityVehicle __instance)
        {
            if (!VehiclePatchHelper.IsLocalPlayerVehicle(__instance)) return;
            HapticsConfig.VehicleDestroyed.Fire();
        }
    }

    /// <summary>
    /// Continuous low rumble while driving fast.
    /// Fires every ~0.2 s when speed exceeds the threshold.
    /// </summary>
    [HarmonyPatch(typeof(EntityVehicle), "updateSteeringAndThrottle")]
    public class VehicleSpeedPatch
    {
        private static double _lastFire;
        private const  float  SpeedThreshold = 12f; // m/s ≈ 43 km/h

        /// <summary>Called by SessionResetPatch on world load (6a fix).</summary>
        public static void ResetTimestamp() => _lastFire = 0.0;

        static void Postfix(EntityVehicle __instance)
        {
            if (!VehiclePatchHelper.IsLocalPlayerVehicle(__instance)) return;
            if (!HapticsConfig.VehicleSpeed.Enabled.Value) return;

            double now   = Time.timeAsDouble;
            float  speed = __instance.speedForward;

            if (speed > SpeedThreshold && now - _lastFire > 0.2)
            {
                float scaled = Mathf.Clamp01((speed - SpeedThreshold) / 20f);
                HapticsConfig.VehicleSpeed.Fire(scaled);
                _lastFire = now;
            }
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    internal static class VehiclePatchHelper
    {
        internal static bool IsLocalPlayerVehicle(EntityVehicle vehicle)
        {
            // AttachedEntities removed in 7DTD 1.x — use GetAttachedPlayerLocal().
            return vehicle.GetAttachedPlayerLocal() != null;
        }
    }
}
