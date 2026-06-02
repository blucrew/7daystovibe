using HarmonyLib;

namespace HapticsPlugin.Patches
{
    // ── Stealth Events ────────────────────────────────────────────────────────

    /// <summary>
    /// Fires when a zombie starts its targeting task and the current attack target is the local player.
    /// In 7DTD 1.x, EAISetNearestTarget was merged into EAITarget; we hook Start() on EAITarget
    /// and filter to events where a non-player entity has locked onto the local player.
    /// </summary>
    [HarmonyPatch(typeof(EAITarget), "Start")]
    public class ZombieDetectsPatch
    {
        static void Postfix(EAITarget __instance)
        {
            if (__instance.theEntity is EntityPlayer) return;           // ignore player-controlled
            var target = __instance.theEntity?.GetAttackTarget();
            if (!(target is EntityPlayerLocal)) return;
            HapticsConfig.ZombieDetects.Fire();
        }
    }

    // 1c fix: AlarmTriggeredPatch removed from here.
    //   The BlockAlarmClock.OnBlockActivated approach fired when the player *sets* an alarm clock,
    //   not when a siren/trap triggers. The correct hook is EntityBuffs.AddBuff (see WorldEventPatches).
    //   Keeping the class here would also cause a compile error — AlarmTriggeredPatch is already
    //   declared in WorldEventPatches.cs.

    /// <summary>
    /// Fires when a wandering horde begins spawning — this is the 7DTD 1.x equivalent of the
    /// screamer triggering a horde. EAIScreamer no longer exists as a separate AI class;
    /// the screamer's "call horde" behaviour is now handled by AIDirectorWanderingHordeComponent.
    /// Note: also fires for non-screamer wandering hordes; the config is labelled "Screamer Spots"
    /// but in practice covers all wandering horde starts.
    /// </summary>
    [HarmonyPatch(typeof(AIDirectorWanderingHordeComponent), "StartSpawning")]
    public class ScreamerSpotsPatch
    {
        static void Postfix()
        {
            HapticsConfig.ScreamerSpots.Fire();
        }
    }

    // ── Bite ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires when the player becomes infected (zombie bite / infection source).
    ///
    /// 1.x fix: DamageSource.DamageTypes was removed in 7DTD 1.x, making it impossible
    /// to reliably detect "bite/poison" damage type from a DamageResponse.
    /// Replaced with a buff-based approach: fire when "buffInfected" is applied to the
    /// local player. This fires once at infection onset — the same moment a bite applies —
    /// which is a better UX trigger than per-tick damage anyway.
    ///
    /// PlayerDamagePatch now fires for all player damage (including bites) so the impact
    /// is still felt; this provides the additional distinctive "infected" pulse.
    /// </summary>
    [HarmonyPatch(typeof(EntityBuffs), "AddBuff")]
    public class PlayerBitePatch
    {
        static void Postfix(EntityBuffs __instance, string _buffName)
        {
            if (!(__instance.parent is EntityPlayerLocal)) return;
            // "buffInfected" is the standard infection buff name in 7DTD.
            // Verify via dnSpy if this doesn't fire on zombie bite.
            if (_buffName == "buffInfected" || _buffName.StartsWith("buffBite"))
                HapticsConfig.PlayerBite.Fire();
        }
    }

    // 1a fix: PlayerGrabbedPatch removed from here.
    //   It was a second Harmony Postfix on EntityPlayer.updateCurrentBiomeAndWeather, the same
    //   method already patched by StatusTickPatch in StatusEffectPatches.cs. Having two independent
    //   patches with separate _lastFire counters allowed the grabbed event to fire at up to double
    //   the intended rate. Grabbed detection is now handled inside StatusTickPatch.

    // ── Headshot / Critical Hit ───────────────────────────────────────────────
    //
    // Both patches target EntityAlive.OnHitResponse. They are mutually exclusive:
    //   HeadshotPatch  fires when  Head + Critical
    //   CriticalPatch  fires when  !Head + Critical
    // So a single headshot critical cannot fire both events.

    /// <summary>Fires when the local player lands a headshot on a zombie.</summary>
    [HarmonyPatch(typeof(EntityAlive), "OnHitResponse")]
    public class HeadshotPatch
    {
        static void Postfix(EntityAlive __instance, DamageResponse _dmResponse)
        {
            if (__instance is EntityPlayer) return;
            // DamageSource.EntityId is not a public property in 7DTD 1.x — use Traverse.
            int sourceId = _dmResponse.Source != null
                ? Traverse.Create(_dmResponse.Source).Field("entityId").GetValue<int>()
                : -1;
            if (sourceId != GameManager.Instance?.World?.GetPrimaryPlayerId()) return;
            if (_dmResponse.HitBodyPart == EnumBodyPartHit.Head && _dmResponse.Critical)
                HapticsConfig.Headshot.Fire();
        }
    }

    /// <summary>Fires when the local player lands a critical (non-headshot) hit.</summary>
    [HarmonyPatch(typeof(EntityAlive), "OnHitResponse")]
    public class CriticalHitPatch
    {
        static void Postfix(EntityAlive __instance, DamageResponse _dmResponse)
        {
            if (__instance is EntityPlayer) return;
            // DamageSource.EntityId is not a public property in 7DTD 1.x — use Traverse.
            int sourceId = _dmResponse.Source != null
                ? Traverse.Create(_dmResponse.Source).Field("entityId").GetValue<int>()
                : -1;
            if (sourceId != GameManager.Instance?.World?.GetPrimaryPlayerId()) return;
            if (_dmResponse.Critical && _dmResponse.HitBodyPart != EnumBodyPartHit.Head)
                HapticsConfig.CriticalHit.Fire();
        }
    }
}
