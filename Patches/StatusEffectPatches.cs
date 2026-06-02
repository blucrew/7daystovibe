using HarmonyLib;
using System;

namespace HapticsPlugin.Patches
{
    // ── Melee ─────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(ItemActionMelee), "HitTarget")]
    public class MeleeHitPatch
    {
        static void Postfix(ItemActionMelee __instance)
        {
            // ActionsHolder removed in 7DTD 1.x — use the primary player singleton instead.
            var player = GameManager.Instance?.World?.GetPrimaryPlayer() as EntityPlayerLocal;
            if (player == null) return;
            if (PatchHelpers.IsSpectator(player)) return;
            HapticsConfig.MeleeHit.Fire();
        }
    }

    // ── Per-frame status ticks ────────────────────────────────────────────────
    // A-2:  Consolidated into a single patch to reduce hooks on one method.
    //       Status ticks, grabbed check, and heal detection all live here.
    //
    // A-15: Static timestamps are reset to 0 on world load via SessionResetPatch.
    //       Time.timeAsDouble resets to 0 on new game — large stored values from a
    //       previous session would block all ticks until timeAsDouble catches up.
    //
    // Healing detection (from bHaptics reference):
    //       Track player health each frame; if HP rises > 5 in one tick, fire PlayerHeal.
    //       This catches healing from any source (stims, medkits, food regen, bandages)
    //       without patching every individual item.

    [HarmonyPatch(typeof(EntityPlayer), "updateCurrentBiomeAndWeather")]
    public class StatusTickPatch
    {
        private static double _lastBleeding;
        private static double _lastFire;
        private static double _lastInfected;
        private static double _lastDrowning;
        private static double _lastStarving;
        private static double _lastOverheat;
        private static double _lastFreezing;
        private static double _lastGrabbed;
        private static double _lastHeal;
        private static float  _lastHealth = -1f;   // < 0 = uninitialised (skip first frame)

        /// <summary>Call on world load to prevent stale timestamps blocking ticks.</summary>
        public static void ResetTimestamps()
        {
            _lastBleeding = _lastFire = _lastInfected = _lastDrowning =
            _lastStarving = _lastOverheat = _lastFreezing = _lastGrabbed =
            _lastHeal = 0.0;
            _lastHealth = -1f;
        }

        static void Postfix(EntityPlayer __instance)
        {
            if (!(__instance is EntityPlayerLocal)) return;

            // Safe downcast: isLocalPlayer guarantees this is EntityPlayerLocal.
            var local = __instance as EntityPlayerLocal;
            if (local == null) return;
            if (PatchHelpers.IsSpectator(local)) return;

            double now   = UnityEngine.Time.timeAsDouble;
            var    buffs = __instance.Buffs;

            // ── Ongoing status effects (rate-limited) ─────────────────────────
            if (buffs.HasBuff("buffBleeding")  && now - _lastBleeding > 1.5)  { HapticsConfig.PlayerBleeding.Fire();    _lastBleeding  = now; }
            if (buffs.HasBuff("buffOnFire")     && now - _lastFire     > 0.5)  { HapticsConfig.PlayerOnFire.Fire();      _lastFire      = now; }
            if (buffs.HasBuff("buffInfected")   && now - _lastInfected > 2.0)  { HapticsConfig.PlayerInfected.Fire();    _lastInfected  = now; }
            // IsDrowning removed in 7DTD 1.x — use buff check instead.
            // "buffDrowning" is the internal buff name; verify via dnSpy if this doesn't fire.
            if (buffs.HasBuff("buffDrowning")   && now - _lastDrowning > 1.0)  { HapticsConfig.PlayerDrowning.Fire();    _lastDrowning  = now; }
            if (__instance.Stats.Food.Value < 20f && now - _lastStarving > 3.0){ HapticsConfig.PlayerStarving.Fire();    _lastStarving  = now; }
            if (buffs.HasBuff("buffHeatStroke") && now - _lastOverheat > 2.0)  { HapticsConfig.PlayerOverheating.Fire(); _lastOverheat  = now; }
            if (buffs.HasBuff("buffFrostbite")  && now - _lastFreezing > 2.0)  { HapticsConfig.PlayerFreezing.Fire();    _lastFreezing  = now; }

            // IsGrabbed is a private field in 7DTD 1.x — use Traverse to read it safely.
            bool isGrabbed = Traverse.Create(__instance).Field("m_IsGrabbed").GetValue<bool>();
            if (isGrabbed && now - _lastGrabbed > 0.5) { HapticsConfig.PlayerGrabbed.Fire(); _lastGrabbed = now; }

            // ── Healing detection ─────────────────────────────────────────────
            // Fires when HP rises by more than 5 in a single tick (any source).
            // Rate-limited to 2 s to avoid a burst of events during fast regeneration.
            float hp = __instance.Health;
            if (_lastHealth >= 0f && hp - _lastHealth > 5f && now - _lastHeal > 2.0)
            {
                // Scale intensity by heal amount: 5 HP = 0.1, 50 HP = 1.0
                float scaled = (float)Math.Max(0.1, Math.Min(1.0, (hp - _lastHealth) / 50.0));
                HapticsConfig.PlayerHeal.Fire(scaled);
                _lastHeal = now;
            }
            _lastHealth = hp;
        }
    }

    // ── Broken bone ───────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(EntityBuffs), "AddBuff")]
    public class BrokenBonePatch
    {
        static void Postfix(EntityBuffs __instance, string _buffName)
        {
            if (!_buffName.StartsWith("buffLegBroke") && !_buffName.StartsWith("buffArmBroke")) return;
            if (__instance.parent is EntityPlayerLocal)
                HapticsConfig.PlayerBrokenBone.Fire();
        }
    }

    // ── Fall landing (speed-scaled) ───────────────────────────────────────────
    // Patching EntityPlayerLocal.FallImpact(float speed) instead of
    // EntityPlayer.OnFallingDamage gives us the raw vertical impact speed,
    // which scales haptic intensity much more naturally than fall distance.
    //
    // Threshold 0.15 m/s matches the OWO reference mod (more meaningful than
    // bHaptics' 0.02 which fires on almost any step).
    //
    // NOTE: verify the method name against your game version using FindMethods.csx.
    //       If "FallImpact" doesn't exist, fall back to OnFallingDamage (see git history).

    [HarmonyPatch(typeof(EntityPlayerLocal), "FallImpact")]
    public class FallImpactPatch
    {
        static void Postfix(EntityPlayerLocal __instance, float speed)
        {
            if (PatchHelpers.IsSpectator(__instance)) return;
            if (speed < 0.15f) return;
            // 0.15 m/s → ~0.05 intensity;  ~15 m/s (fatal drop) → 1.0 intensity
            float scaled = (float)Math.Max(0.05, Math.Min(1.0, (speed - 0.15) / 15.0));
            HapticsConfig.PlayerFallLand.Fire(scaled);
        }
    }

    // ── Respawn ───────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(EntityPlayer), "OnEntitySpawned")]
    public class PlayerRespawnPatch
    {
        private static bool _hasDied;

        // Called by PlayerDeathPatch (A-14 fix)
        public static void MarkDied() => _hasDied = true;

        static void Postfix(EntityPlayer __instance)
        {
            if (!(__instance is EntityPlayerLocal)) return;
            if (!_hasDied) return;
            _hasDied = false; // reset so it fires once per respawn, not every subsequent spawn
            HapticsConfig.PlayerRespawn.Fire();
        }
    }
}
