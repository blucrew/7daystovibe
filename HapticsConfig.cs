using BepInEx.Configuration;
using UnityEngine;

namespace HapticsPlugin
{
    /// <summary>
    /// Central config store. Every event has Enabled, Intensity, Duration, and Pattern.
    /// BepInEx auto-saves these to BepInEx/config/com.rustyblu.7dtd_haptics.cfg
    /// and they also show up in the BepInEx ConfigurationManager overlay (F1) if installed.
    /// </summary>
    public static class HapticsConfig
    {
        // ── GUI ──────────────────────────────────────────────────────────────
        public static ConfigEntry<KeyboardShortcut> GuiToggleKey = null!;
        public static ConfigEntry<float>            GuiScale     = null!;

        // ── Combat ───────────────────────────────────────────────────────────
        public static EventConfig PlayerDamage      = null!;
        public static EventConfig PlayerBite        = null!;
        public static EventConfig PlayerGrabbed     = null!;
        public static EventConfig MeleeHit          = null!;
        public static EventConfig GunShot           = null!;
        public static EventConfig BowShot           = null!;
        public static EventConfig BowDraw           = null!;
        public static EventConfig ZombieKill        = null!;
        public static EventConfig Headshot          = null!;
        public static EventConfig CriticalHit       = null!;
        public static EventConfig BlockBroken       = null!;

        // ── Status Effects ───────────────────────────────────────────────────
        public static EventConfig PlayerHeal        = null!;
        public static EventConfig PlayerEat         = null!;
        public static EventConfig PlayerDrink       = null!;
        public static EventConfig PlayerBleeding    = null!;
        public static EventConfig PlayerBrokenBone  = null!;
        public static EventConfig PlayerOnFire      = null!;
        public static EventConfig PlayerInfected    = null!;
        public static EventConfig PlayerDrowning    = null!;
        public static EventConfig PlayerWaterEnter  = null!;
        public static EventConfig PlayerWaterExit   = null!;
        public static EventConfig PlayerStarving    = null!;
        public static EventConfig PlayerOverheating = null!;
        public static EventConfig PlayerFreezing    = null!;
        public static EventConfig PlayerJump        = null!;
        public static EventConfig PlayerFallLand    = null!;
        public static EventConfig PlayerDeath       = null!;
        public static EventConfig PlayerRespawn     = null!;

        // ── World Events ─────────────────────────────────────────────────────
        public static EventConfig Explosion         = null!;
        public static EventConfig Landmine          = null!;
        public static EventConfig ElectricTrap      = null!;
        public static EventConfig BladeTrap         = null!;
        public static EventConfig AirDrop           = null!;
        public static EventConfig BloodMoonStart    = null!;
        public static EventConfig BloodMoonEnd      = null!;

        // ── Activities ───────────────────────────────────────────────────────
        public static EventConfig Mining            = null!;
        public static EventConfig ChoppingTree      = null!;
        public static EventConfig CraftComplete     = null!;
        public static EventConfig LootOpened        = null!;
        public static EventConfig RareLoot          = null!;
        public static EventConfig LevelUp           = null!;
        public static EventConfig QuestComplete     = null!;

        // ── Vehicles ─────────────────────────────────────────────────────────
        public static EventConfig VehicleCollision  = null!;
        public static EventConfig VehicleDamage     = null!;
        public static EventConfig VehicleDestroyed  = null!;
        public static EventConfig VehicleSpeed      = null!;

        // ── Stealth ──────────────────────────────────────────────────────────
        public static EventConfig ZombieDetects     = null!;
        public static EventConfig AlarmTriggered    = null!;
        public static EventConfig ScreamerSpots     = null!;

        // ── XToys ────────────────────────────────────────────────────────────
        public static ConfigEntry<bool>   XToysEnabled       = null!;
        public static ConfigEntry<string> XToysWebhookId     = null!;
        public static ConfigEntry<float>  XToysMultiplier    = null!;
        public static ConfigEntry<int>    XToysMinDurationMs = null!;

        public static void Init(ConfigFile cfg)
        {
            GuiToggleKey = cfg.Bind("GUI", "ToggleKey",
                new KeyboardShortcut(KeyCode.Insert),
                "Key to open/close the haptics settings panel. Default Insert " +
                "(F7 is avoided — it is the vanilla 'hide HUD' key).");

            GuiScale = cfg.Bind("GUI", "Scale", 0f,
                new ConfigDescription(
                    "Panel UI scale. 0 = auto (fits your screen resolution). " +
                    "Set a value like 1.5, 2.0 or 2.5 to override — bigger = larger panel.",
                    new AcceptableValueRange<float>(0f, 4f)));

            // Helper: bind an event with sensible defaults
            EventConfig E(string section, string key, bool on, float intensity, int duration, HapticPattern pattern)
                => new EventConfig(cfg, section, key, on, intensity, duration, pattern);

            // ── Combat ───────────────────────────────────────────────────────
            PlayerDamage      = E("Combat", "PlayerDamage",      true,  0.8f, 400,  HapticPattern.Vibrate);
            PlayerBite        = E("Combat", "PlayerBite",        true,  0.7f, 600,  HapticPattern.Pulse);
            PlayerGrabbed     = E("Combat", "PlayerGrabbed",     true,  0.4f, 1200, HapticPattern.Vibrate);
            MeleeHit          = E("Combat", "MeleeHit",          true,  0.5f, 150,  HapticPattern.Vibrate);
            GunShot           = E("Combat", "GunShot",           true,  0.25f, 80,   HapticPattern.Vibrate);
            BowShot           = E("Combat", "BowShot",           true,  0.4f,  200,  HapticPattern.Pulse);
            BowDraw           = E("Combat", "BowDraw",           true,  0.3f,  250,  HapticPattern.Vibrate);
            ZombieKill        = E("Combat", "ZombieKill",         true,  0.6f,  200,  HapticPattern.Vibrate);
            Headshot          = E("Combat", "Headshot",           true,  0.9f, 120,  HapticPattern.Vibrate);
            CriticalHit       = E("Combat", "CriticalHit",        true,  0.8f, 180,  HapticPattern.Vibrate);
            BlockBroken       = E("Combat", "BlockBroken",        true,  0.2f, 60,   HapticPattern.Vibrate);

            // ── Status Effects ───────────────────────────────────────────────
            PlayerHeal        = E("Status", "PlayerHeal",        true,  0.3f, 600,  HapticPattern.Pulse);
            PlayerEat         = E("Status", "PlayerEat",         true,  0.2f, 200,  HapticPattern.Vibrate);
            PlayerDrink       = E("Status", "PlayerDrink",       true,  0.2f, 200,  HapticPattern.Vibrate);
            PlayerBleeding    = E("Status", "PlayerBleeding",    true,  0.3f, 800,  HapticPattern.Pulse);
            PlayerBrokenBone  = E("Status", "PlayerBrokenBone",  true,  0.5f, 1000, HapticPattern.Pulse);
            PlayerOnFire      = E("Status", "PlayerOnFire",      true,  0.9f, 500,  HapticPattern.Vibrate);
            PlayerInfected    = E("Status", "PlayerInfected",    true,  0.3f, 800,  HapticPattern.Pulse);
            PlayerDrowning    = E("Status", "PlayerDrowning",    true,  0.6f,  600,  HapticPattern.Pulse);
            PlayerWaterEnter  = E("Status", "PlayerWaterEnter", true,  0.5f,  400,  HapticPattern.Pulse);
            PlayerWaterExit   = E("Status", "PlayerWaterExit",  true,  0.3f,  250,  HapticPattern.Pulse);
            PlayerStarving    = E("Status", "PlayerStarving",    true,  0.3f,  500,  HapticPattern.Pulse);
            PlayerOverheating = E("Status", "PlayerOverheating", true,  0.5f,  600,  HapticPattern.Vibrate);
            PlayerFreezing    = E("Status", "PlayerFreezing",    true,  0.4f,  800,  HapticPattern.Pulse);
            PlayerJump        = E("Status", "PlayerJump",        false, 0.15f, 80,   HapticPattern.Vibrate);
            PlayerFallLand    = E("Status", "PlayerFallLand",    true,  0.7f,  300,  HapticPattern.Vibrate);
            PlayerDeath       = E("Status", "PlayerDeath",       true,  1.0f, 1500, HapticPattern.Pulse);
            PlayerRespawn     = E("Status", "PlayerRespawn",     true,  0.3f, 500,  HapticPattern.Pulse);

            // ── World Events ─────────────────────────────────────────────────
            Explosion         = E("World",  "Explosion",         true,  1.0f, 800,  HapticPattern.Pulse);
            Landmine          = E("World",  "Landmine",          true,  1.0f, 600,  HapticPattern.Vibrate);
            ElectricTrap      = E("World",  "ElectricTrap",      true,  0.7f, 400,  HapticPattern.Vibrate);
            BladeTrap         = E("World",  "BladeTrap",         true,  0.6f, 200,  HapticPattern.Vibrate);
            AirDrop           = E("World",  "AirDrop",           true,  0.5f, 600,  HapticPattern.Pulse);
            BloodMoonStart    = E("World",  "BloodMoonStart",    true,  1.0f, 3000, HapticPattern.Pulse);
            BloodMoonEnd      = E("World",  "BloodMoonEnd",      true,  0.5f, 2000, HapticPattern.Pulse);

            // ── Activities ───────────────────────────────────────────────────
            Mining            = E("Activity", "Mining",          true,  0.2f, 60,   HapticPattern.Vibrate);
            ChoppingTree      = E("Activity", "ChoppingTree",    true,  0.25f, 80,  HapticPattern.Vibrate);
            CraftComplete     = E("Activity", "CraftComplete",   true,  0.4f, 300,  HapticPattern.Pulse);
            LootOpened        = E("Activity", "LootOpened",      true,  0.2f, 150,  HapticPattern.Vibrate);
            RareLoot          = E("Activity", "RareLoot",        true,  0.8f, 500,  HapticPattern.Pulse);
            LevelUp           = E("Activity", "LevelUp",         true,  0.9f, 800,  HapticPattern.Pulse);
            QuestComplete     = E("Activity", "QuestComplete",   true,  0.7f, 600,  HapticPattern.Pulse);

            // ── Vehicles ─────────────────────────────────────────────────────
            VehicleCollision  = E("Vehicle", "VehicleCollision", true,  0.8f, 400,  HapticPattern.Vibrate);
            VehicleDamage     = E("Vehicle", "VehicleDamage",    true,  0.5f, 300,  HapticPattern.Vibrate);
            VehicleDestroyed  = E("Vehicle", "VehicleDestroyed", true,  1.0f, 1000, HapticPattern.Pulse);
            VehicleSpeed      = E("Vehicle", "VehicleSpeed",     false, 0.3f, 100,  HapticPattern.Vibrate);

            // ── Stealth ──────────────────────────────────────────────────────
            ZombieDetects     = E("Stealth", "ZombieDetects",    true,  0.4f, 300,  HapticPattern.Vibrate);
            AlarmTriggered    = E("Stealth", "AlarmTriggered",   true,  0.7f, 500,  HapticPattern.Vibrate);
            ScreamerSpots     = E("Stealth", "ScreamerSpots",    true,  0.9f, 800,  HapticPattern.Pulse);

            // ── XToys ─────────────────────────────────────────────────────────
            XToysEnabled = cfg.Bind("XToys", "Enabled", false,
                "Enable XToys (xtoys.app) webhook output alongside Intiface/Buttplug.\n" +
                "Both outputs fire simultaneously for every event.");

            XToysWebhookId = cfg.Bind("XToys", "WebhookId", "",
                "Your XToys Private Webhook ID.\n" +
                "Get it at: xtoys.app/me -> Private Webhook\n" +
                "You also need xtoys.app open in a browser tab with a script loaded and toy connected.");

            XToysMultiplier = cfg.Bind("XToys", "IntensityMultiplier", 1.0f,
                new ConfigDescription(
                    "Global intensity multiplier applied to all XToys output (0.0–2.0).\n" +
                    "Use values above 1.0 to boost e-stim/Coyote devices that need stronger signals.",
                    new AcceptableValueRange<float>(0f, 2f)));

            XToysMinDurationMs = cfg.Bind("XToys", "MinDurationMs", 300,
                new ConfigDescription(
                    "Minimum event duration sent to XToys (ms). Short events are padded to this length.\n" +
                    "Prevents sub-200ms events (e.g. gun shots at 80ms) arriving too brief to feel\n" +
                    "after ~100-500ms cloud round-trip latency. Default 300ms recommended.",
                    new AcceptableValueRange<int>(100, 2000)));
        }
    }

    public enum HapticPattern      { Vibrate, Pulse }

    /// <summary>
    /// Which physical actuator type to fire on the target device(s).
    /// All = every actuator the device reports (safe default).
    /// Vibrate / Linear / Rotate = only that actuator class.
    /// </summary>
    public enum HapticActuatorType { All, Vibrate, Linear, Rotate }

    /// <summary>
    /// Groups the config entries for one haptic event.
    /// Each instance owns a CancellationTokenSource so that rapid re-fires
    /// cancel the previous still-running motor task before starting the new one.
    /// </summary>
    public class EventConfig
    {
        /// <summary>Config key, used in log messages to identify which event fired.</summary>
        public string                          Name          { get; }
        public ConfigEntry<bool>               Enabled       { get; }
        public ConfigEntry<float>              Intensity     { get; }
        public ConfigEntry<int>                Duration      { get; }
        public ConfigEntry<HapticPattern>      Pattern       { get; }
        /// <summary>Device slot. -1 = all, 0 = first connected, 1 = second, etc.</summary>
        public ConfigEntry<int>                DeviceIndex   { get; }
        /// <summary>Which actuator class to fire. All = every actuator the device has.</summary>
        public ConfigEntry<HapticActuatorType> ActuatorType  { get; }
        /// <summary>
        /// Which actuator index within the chosen type. -1 = all of that type.
        /// e.g. ActuatorType=Vibrate, ActuatorIndex=1 → only the second vibration motor.
        /// </summary>
        public ConfigEntry<int>                ActuatorIndex { get; }

        // Owned per-event CTS — cancelled before each new fire so rapid events
        // don't pile up motor commands or leave motors running too long.
        private System.Threading.CancellationTokenSource? _cts;

        public EventConfig(ConfigFile cfg, string section, string key,
                           bool defaultOn, float defaultIntensity,
                           int defaultDuration, HapticPattern defaultPattern)
        {
            Name = key;
            Enabled       = cfg.Bind(section, $"{key}.Enabled",       defaultOn,              $"Enable {key} haptic feedback.");
            Intensity     = cfg.Bind(section, $"{key}.Intensity",     defaultIntensity,        new ConfigDescription($"Intensity for {key}.", new AcceptableValueRange<float>(0f, 1f)));
            Duration      = cfg.Bind(section, $"{key}.Duration",      defaultDuration,         new ConfigDescription($"Duration (ms) for {key}.", new AcceptableValueRange<int>(50, 5000)));
            Pattern       = cfg.Bind(section, $"{key}.Pattern",       defaultPattern,          "Vibrate = flat buzz. Pulse = ramp up/down.");
            DeviceIndex   = cfg.Bind(section, $"{key}.DeviceIndex",   -1,                      new ConfigDescription(
                                $"Device slot for {key}. -1 = all devices. 0 = first connected, 1 = second, etc.",
                                new AcceptableValueRange<int>(-1, 7)));
            ActuatorType  = cfg.Bind(section, $"{key}.ActuatorType",  HapticActuatorType.All,  $"Actuator class for {key}. All = fire every actuator the device supports.");
            ActuatorIndex = cfg.Bind(section, $"{key}.ActuatorIndex", -1,                      new ConfigDescription(
                                $"Actuator index within the chosen type for {key}. -1 = all of that type. 0 = first, 1 = second, etc.",
                                new AcceptableValueRange<int>(-1, 7)));
        }

        /// <summary>Fire this event — respects all config settings.</summary>
        public void Fire()
        {
            if (!Enabled.Value) return;
            FireWithIntensity(Intensity.Value);
        }

        /// <summary>
        /// Fire with a runtime intensity override (e.g. damage-scaled).
        /// The override is multiplied by the configured Intensity so the slider
        /// acts as a master volume knob.
        /// Uses System.Math.Clamp rather than UnityEngine.Mathf so it is safe to
        /// call from any thread (Harmony postfixes run on the Unity main thread, but
        /// this avoids a Unity-API dependency in the hot path).
        /// </summary>
        public void Fire(float intensityOverride)
        {
            if (!Enabled.Value) return;
            float effective = (float)System.Math.Max(0.0, System.Math.Min(1.0, intensityOverride * (double)Intensity.Value));
            FireWithIntensity(effective);
        }

        /// <summary>
        /// Fire ignoring the Enabled flag — used by the GUI Test button so you can
        /// verify routing even while the event is toggled off.
        /// </summary>
        public void FireForTest() => FireWithIntensity(Intensity.Value);

        private void FireWithIntensity(float intensity)
        {
            HapticsLogger.Verbose(LogCat.Event,
                $"{Name}: intensity={intensity:F2}, dur={Duration.Value}ms, pattern={Pattern.Value}, " +
                $"dev={DeviceIndex.Value}, act={ActuatorType.Value}[{ActuatorIndex.Value}]");

            // ── Intiface / Buttplug output ────────────────────────────────────
            ButtplugManager.Fire(
                intensity,
                Duration.Value,
                Pattern.Value,
                DeviceIndex.Value,
                ActuatorType.Value,
                ActuatorIndex.Value,
                ref _cts);

            // ── XToys output (parallel, fire-and-forget, non-blocking) ────────
            if (HapticsConfig.XToysEnabled?.Value == true)
                XToysManager.Fire(intensity, Duration.Value);
        }
    }
}
