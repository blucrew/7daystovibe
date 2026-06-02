using BepInEx.Configuration;
using HarmonyLib;
using System.Text;
using UnityEngine;

namespace HapticsPlugin
{
    /// <summary>
    /// In-game IMGUI overlay for tweaking haptic settings and viewing the log.
    /// Toggle with the key set in HapticsConfig.GuiToggleKey (default Insert).
    ///
    /// Settings tab
    /// ────────────
    /// Each event row shows:
    ///   [✓] Enable | Intensity | Duration | Pattern | Device ◄► | Actuator ◄► | [Test]
    ///
    /// Log tab
    /// ───────
    /// Category filter | Verbosity selector | Write-to-File toggle | Clear
    /// Scrolling, colour-coded log entries with auto-scroll to bottom on new entries.
    /// </summary>
    public class HapticsGUI : MonoBehaviour
    {
        // ── Layout constants ──────────────────────────────────────────────────
        private const float WinW     = 940f;
        private const float WinH     = 630f;
        private const float RowH     = 28f;
        private const float ColLabel = 142f;
        private const float ColCheck = 30f;
        private const float ColIntW  = 90f;
        private const float ColDurW  = 100f;
        private const float ColPat   = 80f;
        private const float ColDev   = 122f;  // device picker
        private const float ColAct   = 116f;  // actuator picker
        private const float ColTest  = 46f;
        private const float Pad      = 7f;
        private const float LogLineH = 16f;   // height of one log entry line

        private static readonly string[] PatternNames = { "Vibrate", "Pulse" };
        private static readonly Color    RedTint      = HapticsTheme.Danger;

        // ── Log tab filter labels (must match LogCat constants after Trim()) ──
        private static readonly string[] LogFilterNames =
            { "All", "System", "Device", "Buttplug", "XToys", "Event", "Patch" };

        private static readonly string[] VerbosityNames =
            { "Off", "Error", "Warn", "Info", "Verbose" };

        // ── Cached GUIStyles (allocated once in Start, never per-frame) ───────
        private GUIStyle _styleBold       = null!;
        private GUIStyle _styleCatBtn     = null!;
        private GUIStyle _styleDevLabel   = null!;  // separate: device and actuator pickers
        private GUIStyle _styleActLabel   = null!;  //   mutate .normal.textColor independently
        private GUIStyle _styleNote       = null!;
        private GUIStyle _styleStatus     = null!;
        private GUIStyle _styleLogLine    = null!;  // Info level (white)
        private GUIStyle _styleLogWarn    = null!;  // Warning level (yellow)
        private GUIStyle _styleLogError   = null!;  // Error level (red)
        private GUIStyle _styleLogVerbose = null!;  // Verbose level (grey)
        private bool     _initialized;              // guards OnGUI before Start() completes
        private bool     _stylesReady;              // guards lazy GUIStyle creation in OnGUI
        private bool     _positioned;               // window centred for current scale

        // ── State ─────────────────────────────────────────────────────────────
        private bool    _visible;
        private bool    _showLog;
        private bool    _showXToys;
        private Rect    _windowRect;
        private Vector2 _scrollPos;      // settings scroll position
        private bool[]  _catOpen = null!;

        // ── Log tab state ─────────────────────────────────────────────────────
        private Vector2 _logScrollPos;
        private string  _logFilter     = "All"; // "All" or one of LogCat.Xxx trimmed
        private int     _logLastCount  = -1;    // detects new entries for auto-scroll
        private bool    _logAutoScroll = true;  // scroll to bottom on new entries

        private CategoryData[] _categories = null!;

        // Actuator option cache: keyed by deviceIndex, invalidated when DeviceListVersion changes
        private readonly System.Collections.Generic.Dictionary<int, ActuatorOption[]> _actOptCache
            = new System.Collections.Generic.Dictionary<int, ActuatorOption[]>();
        private int _cachedDeviceListVersion = -1;

        // ── MonoBehaviour ─────────────────────────────────────────────────────
        private void Start()
        {
            _windowRect = new Rect(
                (Screen.width  - WinW) * 0.5f,
                (Screen.height - WinH) * 0.5f,
                WinW, WinH);

            // NOTE: GUIStyles are NOT built here. GUI.skin is only valid inside
            // OnGUI(); building styles in Start() throws a NullReferenceException
            // on some Unity versions, which would silently leave the panel unable
            // to draw. Styles are lazily created by EnsureStyles() on first OnGUI.

            _categories = new[]
            {
                new CategoryData("Combat", new[]
                {
                    Row("Player Damage",    HapticsConfig.PlayerDamage),
                    Row("Player Bite",      HapticsConfig.PlayerBite),
                    Row("Player Grabbed",   HapticsConfig.PlayerGrabbed),
                    Row("Melee Hit",        HapticsConfig.MeleeHit),
                    Row("Gun Shot",         HapticsConfig.GunShot),
                    Row("Bow Shot",         HapticsConfig.BowShot),
                    Row("Bow Draw",         HapticsConfig.BowDraw),
                    Row("Zombie Kill",      HapticsConfig.ZombieKill),
                    Row("Headshot",         HapticsConfig.Headshot),
                    Row("Critical Hit",     HapticsConfig.CriticalHit),
                    Row("Block Broken",     HapticsConfig.BlockBroken),
                }),
                new CategoryData("Status Effects", new[]
                {
                    Row("Player Heal",      HapticsConfig.PlayerHeal),
                    Row("Player Eat",       HapticsConfig.PlayerEat),
                    Row("Player Drink",     HapticsConfig.PlayerDrink),
                    Row("Bleeding",         HapticsConfig.PlayerBleeding),
                    Row("Broken Bone",      HapticsConfig.PlayerBrokenBone),
                    Row("On Fire",          HapticsConfig.PlayerOnFire),
                    Row("Infected",         HapticsConfig.PlayerInfected),
                    Row("Drowning",         HapticsConfig.PlayerDrowning),
                    Row("Water Enter",      HapticsConfig.PlayerWaterEnter),
                    Row("Water Exit",       HapticsConfig.PlayerWaterExit),
                    Row("Starving",         HapticsConfig.PlayerStarving),
                    Row("Overheating",      HapticsConfig.PlayerOverheating),
                    Row("Freezing",         HapticsConfig.PlayerFreezing),
                    Row("Jump",             HapticsConfig.PlayerJump),
                    Row("Fall Landing",     HapticsConfig.PlayerFallLand),
                    Row("Player Death",     HapticsConfig.PlayerDeath),
                    Row("Player Respawn",   HapticsConfig.PlayerRespawn),
                }),
                new CategoryData("World Events", new[]
                {
                    Row("Explosion",        HapticsConfig.Explosion),
                    Row("Landmine",         HapticsConfig.Landmine),
                    Row("Electric Trap",    HapticsConfig.ElectricTrap),
                    Row("Blade Trap",       HapticsConfig.BladeTrap),
                    Row("Air Drop",         HapticsConfig.AirDrop),
                    Row("Blood Moon Start", HapticsConfig.BloodMoonStart),
                    Row("Blood Moon End",   HapticsConfig.BloodMoonEnd),
                }),
                new CategoryData("Activities", new[]
                {
                    Row("Mining",           HapticsConfig.Mining),
                    Row("Chopping Tree",    HapticsConfig.ChoppingTree),
                    Row("Craft Complete",   HapticsConfig.CraftComplete),
                    Row("Loot Opened",      HapticsConfig.LootOpened),
                    Row("Rare Loot",        HapticsConfig.RareLoot),
                    Row("Level Up",         HapticsConfig.LevelUp),
                    Row("Quest Complete",   HapticsConfig.QuestComplete),
                }),
                new CategoryData("Vehicles", new[]
                {
                    Row("Vehicle Collision",HapticsConfig.VehicleCollision),
                    Row("Vehicle Damage",   HapticsConfig.VehicleDamage),
                    Row("Vehicle Destroyed",HapticsConfig.VehicleDestroyed),
                    Row("Vehicle Speed",    HapticsConfig.VehicleSpeed),
                }),
                new CategoryData("Stealth", new[]
                {
                    Row("Zombie Detects",   HapticsConfig.ZombieDetects),
                    Row("Alarm Triggered",  HapticsConfig.AlarmTriggered),
                    Row("Screamer Spots",   HapticsConfig.ScreamerSpots),
                }),
            };

            _catOpen = new bool[_categories.Length];
            for (int i = 0; i < _catOpen.Length; i++) _catOpen[i] = true;

            _initialized = true;  // Start completed — OnGUI may now render
            HapticsLogger.Info(LogCat.System,
                $"GUI ready ({_categories.Length} categories). Toggle key: {HapticsConfig.GuiToggleKey.Value}");
        }

        /// <summary>
        /// Build the cached GUIStyles. Called from OnGUI (where GUI.skin is valid),
        /// guarded so it only runs once. Colours pull from HapticsTheme (OLED palette).
        /// </summary>
        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _styleBold      = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = HapticsTheme.Muted } };
            _styleCatBtn    = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = HapticsTheme.Fg } };
            _styleDevLabel  = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10, normal = { textColor = HapticsTheme.Fg } };
            _styleActLabel  = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10, normal = { textColor = HapticsTheme.Fg } };
            _styleNote      = new GUIStyle(GUI.skin.label) { fontSize = 10, normal = { textColor = HapticsTheme.Muted } };
            _styleStatus    = new GUIStyle(GUI.skin.label) { fontSize = 10 };
            _styleLogLine    = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Normal, normal = { textColor = HapticsTheme.Fg     } };
            _styleLogWarn    = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = HapticsTheme.Warn   } };
            _styleLogError   = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = HapticsTheme.Danger } };
            _styleLogVerbose = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = HapticsTheme.Muted  } };

            _stylesReady = true;
        }

        private void Update()
        {
            if (HapticsConfig.GuiToggleKey.Value.IsDown())
            {
                _visible = !_visible;
                HapticsLogger.Info(LogCat.System, $"Panel toggled {( _visible ? "OPEN" : "closed")}.");
                ApplyCursorState(_visible);
            }

            // Re-assert every frame while open — 7DTD's own input loop re-locks the
            // cursor and re-captures look/move each Update, so a one-shot toggle is
            // not enough. SetCursorEnabledOverride(true) puts the game in UI mode
            // (cursor shown, player look/move suppressed) like a vanilla menu.
            if (_visible)
                ApplyCursorState(true);
        }

        /// <summary>
        /// Free or recapture the mouse for the overlay. Uses 7DTD's own
        /// GameManager.SetCursorEnabledOverride (via Traverse so signature/access
        /// changes don't break the build) and forces the hardware cursor as a fallback.
        /// </summary>
        private static void ApplyCursorState(bool show)
        {
            try
            {
                var gm = GameManager.Instance;
                if (gm != null)
                    Traverse.Create(gm).Method("SetCursorEnabledOverride", new object[] { show }).GetValue();
            }
            catch { /* method shape changed — cursor fallback below still applies */ }

            Cursor.visible   = show;
            Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
        }

        private void OnGUI()
        {
            if (!_initialized || !_visible) return;  // guard against pre-Start() calls

            EnsureStyles();  // build GUIStyles now that GUI.skin is valid

            // ── High-DPI scaling ──────────────────────────────────────────────
            // IMGUI draws at native pixels, so a fixed 940px window looks tiny on
            // 1440p/4K. Scale the entire GUI via GUI.matrix; mouse input is
            // transformed automatically, so dragging/clicks still line up.
            float scale = UiScale();
            float vw    = Screen.width  / scale;   // viewport size in scaled coords
            float vh    = Screen.height / scale;

            // Centre the window once (and re-centre if it would sit off-screen,
            // e.g. after a scale change).
            if (!_positioned || _windowRect.x > vw - 40f || _windowRect.y > vh - 40f)
            {
                _windowRect.x = Mathf.Max(0f, (vw - WinW) * 0.5f);
                _windowRect.y = Mathf.Max(0f, (vh - WinH) * 0.5f);
                _positioned = true;
            }

            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            // Dim backdrop across the full (scaled) viewport.
            GUI.color = new Color(HapticsTheme.Ink.r, HapticsTheme.Ink.g, HapticsTheme.Ink.b, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, vw, vh), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Apply the themed skin for the duration of the window, then restore.
            GUISkin prevSkin = GUI.skin;
            GUI.skin = HapticsTheme.GetSkin(prevSkin);
            _windowRect = GUI.Window(9999, _windowRect, DrawWindow, "  7DTD HAPTICS");
            GUI.skin = prevSkin;

            GUI.matrix = prevMatrix;
        }

        /// <summary>
        /// Effective UI scale. Config "GUI.Scale" overrides when > 0; otherwise
        /// auto-fit from screen height (≈1× at 900p, ~1.6× at 1440p, capped at 3×).
        /// </summary>
        private static float UiScale()
        {
            float cfg = HapticsConfig.GuiScale?.Value ?? 0f;
            if (cfg > 0.01f) return cfg;
            return Mathf.Clamp(Screen.height / 900f, 1f, 3f);
        }

        // ── Main window ───────────────────────────────────────────────────────
        private void DrawWindow(int id)
        {
            float y = 22f;

            // Tab row: Settings | XToys | Log
            int tabIdx = _showLog ? 2 : (_showXToys ? 1 : 0);
            int newTab  = GUI.SelectionGrid(new Rect(Pad, y, 240f, 22f), tabIdx,
                                            new[] { "Settings", "XToys", "Log" }, 3);
            if (newTab != tabIdx)
            {
                _showXToys = newTab == 1;
                _showLog   = newTab == 2;
            }
            y += 26f;

            if (_showLog)
                DrawLogTab(y);
            else if (_showXToys)
                DrawXToysTab(y);
            else
                DrawSettingsTab(y);

            GUI.DragWindow(new Rect(0, 0, WinW, 22f));
        }

        // ── Settings tab ──────────────────────────────────────────────────────
        private void DrawSettingsTab(float y)
        {
            DrawHeaders(y);
            y += RowH;

            float bodyH = _windowRect.height - y - 56f;
            Rect viewRect = new Rect(0, 0, WinW - 20f, ContentHeight());
            _scrollPos = GUI.BeginScrollView(new Rect(0, y, WinW, bodyH), _scrollPos, viewRect);

            float cy = 0f;
            for (int ci = 0; ci < _categories.Length; ci++)
                cy = DrawCategory(ci, cy);

            GUI.EndScrollView();

            float footY = _windowRect.height - 52f;
            DrawDeviceStatusBar(footY);
            footY += 26f;

            if (GUI.Button(new Rect(Pad, footY, 110f, 22f), "Collapse All"))
                for (int i = 0; i < _catOpen.Length; i++) _catOpen[i] = false;
            if (GUI.Button(new Rect(Pad + 118f, footY, 110f, 22f), "Expand All"))
                for (int i = 0; i < _catOpen.Length; i++) _catOpen[i] = true;

            GUI.Label(new Rect(WinW - 270f, footY + 3f, 260f, 20f),
                      "Changes save automatically  |  Insert to close", _styleNote);
        }

        // ── XToys tab ─────────────────────────────────────────────────────────
        private void DrawXToysTab(float y)
        {
            const float CtrlH  = 26f;
            const float LabelW = 160f;

            // ── Enable toggle ──────────────────────────────────────────────────
            bool xtEnabled = GUI.Toggle(new Rect(Pad, y + 4f, 220f, 20f),
                                        HapticsConfig.XToysEnabled?.Value ?? false,
                                        " Enable XToys output");
            if (HapticsConfig.XToysEnabled != null && xtEnabled != HapticsConfig.XToysEnabled.Value)
                HapticsConfig.XToysEnabled.Value = xtEnabled;
            y += CtrlH + 4f;

            // ── Webhook ID (display-only — edit in .cfg; avoids game keyboard conflicts) ──
            GUI.Label(new Rect(Pad, y + 4f, LabelW, CtrlH), "Webhook ID:", _styleBold);
            string idVal     = HapticsConfig.XToysWebhookId?.Value ?? "";
            string idDisplay = string.IsNullOrWhiteSpace(idVal)
                ? "— not set —  (add to BepInEx/config/*.cfg)"
                : $"{idVal.Substring(0, System.Math.Min(6, idVal.Length))}…  ✓ set  ({idVal.Length} chars)";
            _styleNote.normal.textColor = string.IsNullOrWhiteSpace(idVal)
                ? HapticsTheme.Warn : HapticsTheme.Accent;
            GUI.Label(new Rect(Pad + LabelW + 4f, y + 4f, WinW - LabelW - 20f, CtrlH), idDisplay, _styleNote);
            _styleNote.normal.textColor = HapticsTheme.Muted; // reset
            y += CtrlH;

            // ── Intensity Multiplier ───────────────────────────────────────────
            if (HapticsConfig.XToysMultiplier != null)
            {
                GUI.Label(new Rect(Pad, y + 4f, LabelW, CtrlH), "Intensity ×:", _styleBold);
                float mult = GUI.HorizontalSlider(
                    new Rect(Pad + LabelW + 4f, y + 12f, 260f, 16f),
                    HapticsConfig.XToysMultiplier.Value, 0f, 2f);
                mult = Mathf.Round(mult * 20f) / 20f; // snap to 0.05 steps
                if (Mathf.Abs(mult - HapticsConfig.XToysMultiplier.Value) > 0.01f)
                    HapticsConfig.XToysMultiplier.Value = mult;
                GUI.Label(new Rect(Pad + LabelW + 272f, y + 4f, 50f, CtrlH), $"{mult:F2}×");
                y += CtrlH;
            }

            // ── Min Duration ──────────────────────────────────────────────────
            if (HapticsConfig.XToysMinDurationMs != null)
            {
                GUI.Label(new Rect(Pad, y + 4f, LabelW, CtrlH), "Min Duration:", _styleBold);
                float minDur = GUI.HorizontalSlider(
                    new Rect(Pad + LabelW + 4f, y + 12f, 260f, 16f),
                    HapticsConfig.XToysMinDurationMs.Value, 100f, 2000f);
                int minDurInt = Mathf.RoundToInt(minDur / 50f) * 50;
                if (minDurInt != HapticsConfig.XToysMinDurationMs.Value)
                    HapticsConfig.XToysMinDurationMs.Value = minDurInt;
                GUI.Label(new Rect(Pad + LabelW + 272f, y + 4f, 60f, CtrlH), $"{minDurInt}ms");
                y += CtrlH + 8f;
            }

            // ── Test button ───────────────────────────────────────────────────
            bool canTest = XToysManager.IsEnabled;
            GUI.enabled  = canTest;
            if (GUI.Button(new Rect(Pad, y, 170f, 24f), "Test  (50% for 1 second)"))
                _ = XToysManager.FireRawAsync(50, 1000);
            GUI.enabled = true;
            if (!canTest)
                GUI.Label(new Rect(Pad + 178f, y + 4f, 320f, 20f),
                          "Enable XToys and set a Webhook ID first", _styleNote);
            y += 34f;

            // ── Status bar ────────────────────────────────────────────────────
            var tint = GUI.color;
            GUI.color = HapticsTheme.Panel2;
            GUI.DrawTexture(new Rect(0f, y, WinW, 24f), Texture2D.whiteTexture);
            GUI.color = tint;

            string statusMsg;
            Color  statusCol;
            if (!xtEnabled)
            {
                statusMsg = "●  XToys output disabled.";
                statusCol = HapticsTheme.Muted;
            }
            else if (string.IsNullOrWhiteSpace(HapticsConfig.XToysWebhookId?.Value))
            {
                statusMsg = "⚠  Enabled but no Webhook ID — set XToys.WebhookId in BepInEx/config/*.cfg";
                statusCol = HapticsTheme.Warn;
            }
            else
            {
                statusMsg = "✓  XToys ready — fires on every event alongside Intiface. " +
                            "Keep xtoys.app open in a browser tab with a script loaded.";
                statusCol = HapticsTheme.Accent;
            }
            _styleStatus.normal.textColor = statusCol;
            GUI.Label(new Rect(Pad, y + 4f, WinW - Pad * 2f, 18f), statusMsg, _styleStatus);
            y += 32f;

            // ── Setup guide ───────────────────────────────────────────────────
            var tint2 = GUI.color;
            GUI.color = HapticsTheme.Ink;
            GUI.DrawTexture(new Rect(0f, y, WinW, _windowRect.height - y - 6f), Texture2D.whiteTexture);
            GUI.color = tint2;

            string[] guide =
            {
                "SETUP  (one-time, ~2 minutes):",
                "  1.  Sign in at  xtoys.app",
                "  2.  Click your profile icon (top-right)  →  Private Webhook  →  copy your Webhook ID",
                "       Note: this ID is tied to your account, not per-script. Guard it like a password.",
                "  3.  Open  BepInEx/config/com.7daystovibe.haptics.cfg  and paste it into:  XToys.WebhookId = <paste here>",
                "  4.  Set  XToys.Enabled = true  in the same file, then restart the game (or toggle Enable above)",
                "  5.  At xtoys.app, search the script library for  '7DTD Haptics'  and load it.",
                "       Inside the script, click  'Add Device'  under the Generic Output block and select your toy.",
                "       The webhook routing is pre-configured — that is all you need to do.",
                "  6.  Keep the xtoys.app browser tab open and visible while playing",
                "",
                "NOTES:",
                "  ●  XToys fires over the internet — expect ~100–500ms latency vs Intiface's <5ms local",
                "  ●  Both Intiface and XToys fire simultaneously — Intiface = local vibrators/thrusters,",
                "       XToys = cloud-connected toys like DG-Lab Coyote (e-stim)",
                "  ●  HTTP 200 from XToys = webhook received. Does NOT confirm the toy responded.",
                "  ●  Intensity × > 1.0 useful for e-stim devices that need stronger signals than vibrators",
                "  ●  Min Duration pads short events (gun shots = 80ms) to feel through cloud latency",
                "  ●  If you regenerate your Webhook ID on xtoys.app you must update the .cfg and restart",
                "  ●  Changes to Intensity ×, Min Duration, and Enable save automatically",
            };
            y += 6f;
            foreach (string line in guide)
            {
                GUI.Label(new Rect(Pad + 4f, y, WinW - Pad * 2f - 8f, 17f), line, _styleNote);
                y += 17f;
            }
        }

        // ── Log tab ───────────────────────────────────────────────────────────
        private void DrawLogTab(float y)
        {
            const float CtrlH = 24f;

            // ── Row 1: category filter + Clear button ─────────────────────────
            GUI.Label(new Rect(Pad, y + 4f, 44f, CtrlH), "Filter:", _styleBold);
            float bx = Pad + 50f;
            foreach (string cat in LogFilterNames)
            {
                bool active = _logFilter == cat;
                if (active)
                {
                    // Draw an accent highlight behind the active filter button
                    var prev = GUI.color;
                    GUI.color = HapticsTheme.AccentLo;
                    GUI.DrawTexture(new Rect(bx - 1f, y + 1f, 62f, 22f), Texture2D.whiteTexture);
                    GUI.color = prev;
                }
                if (GUI.Button(new Rect(bx, y + 2f, 60f, 20f), cat))
                    _logFilter = cat;
                bx += 64f;
            }
            if (GUI.Button(new Rect(WinW - Pad - 72f, y + 2f, 70f, 20f), "Clear"))
                HapticsLogger.Clear();
            y += CtrlH + 2f;

            // ── Row 2: verbosity selector + write-to-file toggle ──────────────
            if (HapticsLogger.Verbosity != null)
            {
                GUI.Label(new Rect(Pad, y + 4f, 72f, CtrlH), "Verbosity:", _styleBold);
                int vIdx = (int)HapticsLogger.Verbosity.Value;
                int newV  = GUI.SelectionGrid(new Rect(Pad + 78f, y + 2f, 290f, 20f), vIdx, VerbosityNames, 5);
                if (newV != vIdx) HapticsLogger.Verbosity.Value = (LogVerbosity)newV;
            }
            if (HapticsLogger.WriteToFile != null)
            {
                bool wf = GUI.Toggle(new Rect(Pad + 382f, y + 4f, 130f, 20f),
                                     HapticsLogger.WriteToFile.Value, " Write to File");
                if (wf != HapticsLogger.WriteToFile.Value)
                    HapticsLogger.WriteToFile.Value = wf;
            }
            y += CtrlH + 3f;

            // Thin divider line
            var tint = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(0f, y, WinW, 1f), Texture2D.whiteTexture);
            GUI.color = tint;
            y += 3f;

            // ── Log scroll view ───────────────────────────────────────────────
            var snapshot = HapticsLogger.GetSnapshot();
            int total    = HapticsLogger.TotalCount;

            // Auto-scroll to bottom when new entries arrive
            bool didAutoScroll = false;
            if (total != _logLastCount)
            {
                _logLastCount = total;
                if (_logAutoScroll)
                {
                    _logScrollPos.y = float.MaxValue;
                    didAutoScroll   = true;
                }
            }

            // Filter entries
            var filtered = new System.Collections.Generic.List<LogEntry>(snapshot.Length);
            foreach (var e in snapshot)
            {
                if (_logFilter == "All" || e.Category.Trim() == _logFilter)
                    filtered.Add(e);
            }

            float availH    = _windowRect.height - y - 6f;
            float innerH    = Mathf.Max(availH, filtered.Count * LogLineH + 4f);
            Rect  outerRect = new Rect(0f, y, WinW, availH);
            Rect  innerRect = new Rect(0f, 0f, WinW - 20f, innerH);

            _logScrollPos = GUI.BeginScrollView(outerRect, _logScrollPos, innerRect);

            // Detect user scroll-up (only when we didn't just auto-scroll this frame)
            if (!didAutoScroll)
            {
                float scrollMax = Mathf.Max(0f, innerH - availH);
                if (_logScrollPos.y < scrollMax - 10f)
                    _logAutoScroll = false;
                if (_logScrollPos.y >= scrollMax - 2f)
                    _logAutoScroll = true;
            }

            float ly = 2f;
            foreach (var e in filtered)
            {
                GUIStyle style = e.Level switch
                {
                    LogVerbosity.Error   => _styleLogError,
                    LogVerbosity.Warning => _styleLogWarn,
                    LogVerbosity.Verbose => _styleLogVerbose,
                    _                    => _styleLogLine,
                };
                string line = $"{e.Time:HH:mm:ss.fff}  {LogLevelTag(e.Level)}  [{e.Category}]  {e.Message}";
                GUI.Label(new Rect(4f, ly, WinW - 28f, LogLineH), line, style);
                ly += LogLineH;
            }

            GUI.EndScrollView();
        }

        private static string LogLevelTag(LogVerbosity v) => v switch
        {
            LogVerbosity.Verbose => "VRB",
            LogVerbosity.Info    => "INF",
            LogVerbosity.Warning => "WRN",
            LogVerbosity.Error   => "ERR",
            _                    => "???",
        };

        // ── Headers ───────────────────────────────────────────────────────────
        private void DrawHeaders(float y)
        {
            float x = Pad;
            GUI.Label(new Rect(x, y, ColLabel, RowH), "Event",       _styleBold); x += ColLabel + Pad;
            GUI.Label(new Rect(x, y, 60f,      RowH), "On",          _styleBold); x += ColCheck + 36f + Pad;
            GUI.Label(new Rect(x, y, 90f,      RowH), "Intensity",   _styleBold); x += ColIntW  + 26f + Pad;
            GUI.Label(new Rect(x, y, 100f,     RowH), "Duration ms", _styleBold); x += ColDurW  + 28f + Pad;
            GUI.Label(new Rect(x, y, 94f,      RowH), "Pattern",     _styleBold); x += ColPat   + 14f + Pad;
            GUI.Label(new Rect(x, y, ColDev,   RowH), "Device",      _styleBold); x += ColDev   + Pad;
            GUI.Label(new Rect(x, y, ColAct,   RowH), "Actuator",    _styleBold);
        }

        // ── Category block ────────────────────────────────────────────────────
        private float DrawCategory(int ci, float y)
        {
            var cat = _categories[ci];
            var prev = GUI.color;

            // Header bar: panel2 fill with a 3px accent rail down the left edge.
            GUI.color = HapticsTheme.Panel2;
            GUI.DrawTexture(new Rect(0, y, WinW - 20f, RowH), Texture2D.whiteTexture);
            GUI.color = _catOpen[ci] ? HapticsTheme.Accent : HapticsTheme.Line;
            GUI.DrawTexture(new Rect(0, y, 3f, RowH), Texture2D.whiteTexture);
            GUI.color = prev;

            // Accent the chevron, keep the label crisp white.
            _styleCatBtn.normal.textColor = _catOpen[ci] ? HapticsTheme.Fg : HapticsTheme.Muted;
            string arrow = _catOpen[ci] ? "▼" : "►";
            if (GUI.Button(new Rect(0, y, WinW - 20f, RowH), $"   {arrow}   {cat.Name}", _styleCatBtn))
                _catOpen[ci] = !_catOpen[ci];
            y += RowH + 2f;
            if (!_catOpen[ci]) return y;

            foreach (var row in cat.Rows) { y = DrawEventRow(row, y); y += 2f; }
            y += 4f;
            return y;
        }

        // ── Event row ─────────────────────────────────────────────────────────
        private float DrawEventRow(EventRow row, float y)
        {
            float x = Pad;

            // Label
            GUI.Label(new Rect(x, y + 5f, ColLabel, RowH), row.Label);
            x += ColLabel + Pad;

            // Enable toggle
            bool enabled = GUI.Toggle(new Rect(x + 4f, y + 6f, ColCheck, ColCheck), row.Cfg.Enabled.Value, "");
            if (enabled != row.Cfg.Enabled.Value) row.Cfg.Enabled.Value = enabled;
            x += ColCheck + 36f + Pad;

            bool wasEnabled = GUI.enabled;
            GUI.enabled = enabled;

            // Intensity slider
            float intens = GUI.HorizontalSlider(new Rect(x, y + 12f, ColIntW, 16f), row.Cfg.Intensity.Value, 0f, 1f);
            intens = Mathf.Round(intens * 100f) / 100f;
            if (Mathf.Abs(intens - row.Cfg.Intensity.Value) > 0.005f) row.Cfg.Intensity.Value = intens;
            GUI.Label(new Rect(x + ColIntW + 2f, y + 5f, 24f, RowH), $"{intens:F2}");
            x += ColIntW + 26f + Pad;

            // Duration slider
            float dur = GUI.HorizontalSlider(new Rect(x, y + 12f, ColDurW, 16f), row.Cfg.Duration.Value, 50f, 5000f);
            int durInt = Mathf.RoundToInt(dur / 50f) * 50;
            if (durInt != row.Cfg.Duration.Value) row.Cfg.Duration.Value = durInt;
            GUI.Label(new Rect(x + ColDurW + 2f, y + 5f, 26f, RowH), $"{durInt}");
            x += ColDurW + 28f + Pad;

            // Pattern
            int patIdx = (int)row.Cfg.Pattern.Value;
            int newPat = GUI.SelectionGrid(new Rect(x, y + 4f, ColPat + 14f, RowH - 6f), patIdx, PatternNames, 2);
            if (newPat != patIdx) row.Cfg.Pattern.Value = (HapticPattern)newPat;
            x += ColPat + 14f + Pad;

            GUI.enabled = wasEnabled;

            // Device picker
            DrawDevicePicker(row.Cfg, x, y);
            x += ColDev + Pad;

            // Actuator picker
            DrawActuatorPicker(row.Cfg, x, y);
            x += ColAct + Pad;

            // Test button — always active, calls FireForTest() which bypasses Enabled
            // so routing can be verified even while the event is toggled off.
            GUI.enabled = true;
            if (GUI.Button(new Rect(x, y + 4f, ColTest, RowH - 6f), "Test"))
                row.Cfg.FireForTest();
            GUI.enabled = wasEnabled;

            return y + RowH;
        }

        // ── Device picker  ◄  All / #0 name / #1 name  ► ─────────────────────
        private void DrawDevicePicker(EventConfig cfg, float x, float y)
        {
            const float BtnW = 18f;
            int current  = cfg.DeviceIndex.Value;
            int devCount = ButtplugManager.DeviceCount;

            // Disable arrows when no devices are connected — only "All" is valid
            bool wasEnabled = GUI.enabled;
            GUI.enabled = devCount > 0;

            if (GUI.Button(new Rect(x, y + 5f, BtnW, RowH - 8f), "◄"))
            {
                int max = Mathf.Max(devCount - 1, 0);
                current = (current <= -1) ? max : current - 1;
                cfg.DeviceIndex.Value = current;
            }

            GUI.enabled = wasEnabled;
            float labelW = ColDev - BtnW * 2f - 4f;
            string label = BuildDeviceLabel(current, devCount);
            bool live = (current < 0) || (current < devCount);
            _styleDevLabel.normal.textColor = live ? Color.white : RedTint;
            GUI.Label(new Rect(x + BtnW + 2f, y, labelW, RowH), label, _styleDevLabel);

            GUI.enabled = devCount > 0;
            if (GUI.Button(new Rect(x + ColDev - BtnW, y + 5f, BtnW, RowH - 8f), "►"))
            {
                int max = Mathf.Max(devCount - 1, 0);
                current = (current >= max) ? -1 : current + 1;
                cfg.DeviceIndex.Value = current;
            }
            GUI.enabled = wasEnabled;
        }

        private static string BuildDeviceLabel(int index, int devCount)
        {
            if (index < 0) return "All";
            string name = ButtplugManager.GetDeviceName(index);
            if (name.Length > 12) name = name.Substring(0, 11) + "…";
            return $"#{index}: {name}";
        }

        // ── Actuator picker  ◄  All / Vib:All / Vib:0 / Lin:All …  ► ────────
        //
        // Options are cached per deviceIndex and rebuilt only when DeviceListVersion changes.
        // ──────────────────────────────────────────────────────────────────────
        private void DrawActuatorPicker(EventConfig cfg, float x, float y)
        {
            const float BtnW = 18f;

            // Invalidate cache when devices connect/disconnect
            if (ButtplugManager.DeviceListVersion != _cachedDeviceListVersion)
            {
                _actOptCache.Clear();
                _cachedDeviceListVersion = ButtplugManager.DeviceListVersion;
            }

            int devIdx = cfg.DeviceIndex.Value;
            if (!_actOptCache.TryGetValue(devIdx, out var opts))
            {
                opts = BuildActuatorOptions(devIdx);
                _actOptCache[devIdx] = opts;
            }

            // Find current position in the cycle
            int curIdx = FindActuatorOption(opts, cfg.ActuatorType.Value, cfg.ActuatorIndex.Value);

            if (GUI.Button(new Rect(x, y + 5f, BtnW, RowH - 8f), "◄"))
            {
                int prev = (curIdx <= 0) ? opts.Length - 1 : curIdx - 1;
                ApplyActuatorOption(cfg, opts[prev]);
            }

            float labelW = ColAct - BtnW * 2f - 4f;
            string label  = curIdx >= 0 ? opts[curIdx].Label : BuildActuatorLabel(cfg.ActuatorType.Value, cfg.ActuatorIndex.Value);

            // Show red if the selected actuator type isn't supported by the device
            bool supported = IsActuatorSupported(cfg.DeviceIndex.Value, cfg.ActuatorType.Value, cfg.ActuatorIndex.Value);
            _styleActLabel.normal.textColor = supported ? Color.white : RedTint;
            GUI.Label(new Rect(x + BtnW + 2f, y, labelW, RowH), label, _styleActLabel);

            if (GUI.Button(new Rect(x + ColAct - BtnW, y + 5f, BtnW, RowH - 8f), "►"))
            {
                int next = (curIdx < 0 || curIdx >= opts.Length - 1) ? 0 : curIdx + 1;
                ApplyActuatorOption(cfg, opts[next]);
            }
        }

        // ── Actuator option list builder ──────────────────────────────────────

        private struct ActuatorOption
        {
            public HapticActuatorType Type;
            public int                Index;  // -1 = all of type
            public string             Label;
        }

        private ActuatorOption[] BuildActuatorOptions(int deviceIndex)
        {
            var info = ButtplugManager.GetDeviceInfo(deviceIndex);
            var list = new System.Collections.Generic.List<ActuatorOption>();

            // Always first: All actuators
            list.Add(new ActuatorOption { Type = HapticActuatorType.All, Index = -1, Label = "All" });

            if (info.HasVibrate || deviceIndex < 0)
            {
                list.Add(new ActuatorOption { Type = HapticActuatorType.Vibrate, Index = -1, Label = "Vib: All" });
                int motors = Mathf.Max(info.VibrateMotors, 1);
                for (int i = 0; i < motors; i++)
                    list.Add(new ActuatorOption { Type = HapticActuatorType.Vibrate, Index = i,
                                                  Label = motors > 1 ? $"Vib: #{i}" : "Vib: 0" });
            }
            if (info.HasLinear || deviceIndex < 0)
            {
                list.Add(new ActuatorOption { Type = HapticActuatorType.Linear, Index = -1, Label = "Lin: All" });
                int acts = Mathf.Max(info.LinearActuators, 1);
                for (int i = 0; i < acts; i++)
                    list.Add(new ActuatorOption { Type = HapticActuatorType.Linear, Index = i,
                                                  Label = acts > 1 ? $"Lin: #{i}" : "Lin: 0" });
            }
            if (info.HasRotate || deviceIndex < 0)
            {
                list.Add(new ActuatorOption { Type = HapticActuatorType.Rotate, Index = -1, Label = "Rot: All" });
                int acts = Mathf.Max(info.RotateActuators, 1);
                for (int i = 0; i < acts; i++)
                    list.Add(new ActuatorOption { Type = HapticActuatorType.Rotate, Index = i,
                                                  Label = acts > 1 ? $"Rot: #{i}" : "Rot: 0" });
            }

            return list.ToArray();
        }

        private static int FindActuatorOption(ActuatorOption[] opts, HapticActuatorType type, int index)
        {
            for (int i = 0; i < opts.Length; i++)
                if (opts[i].Type == type && opts[i].Index == index) return i;
            return 0; // default to "All" if not found
        }

        private static void ApplyActuatorOption(EventConfig cfg, ActuatorOption opt)
        {
            cfg.ActuatorType .Value = opt.Type;
            cfg.ActuatorIndex.Value = opt.Index;
        }

        private static string BuildActuatorLabel(HapticActuatorType type, int index)
        {
            string typeName = type switch
            {
                HapticActuatorType.Vibrate => "Vib",
                HapticActuatorType.Linear  => "Lin",
                HapticActuatorType.Rotate  => "Rot",
                _                          => "All",
            };
            return type == HapticActuatorType.All ? "All"
                 : index < 0 ? $"{typeName}: All"
                 : $"{typeName}: #{index}";
        }

        private static bool IsActuatorSupported(int deviceIndex, HapticActuatorType type, int index)
        {
            if (type == HapticActuatorType.All) return true;
            var info = ButtplugManager.GetDeviceInfo(deviceIndex);
            return type switch
            {
                HapticActuatorType.Vibrate => info.HasVibrate && (index < 0 || index < info.VibrateMotors),
                HapticActuatorType.Linear  => info.HasLinear  && (index < 0 || index < info.LinearActuators),
                HapticActuatorType.Rotate  => info.HasRotate  && (index < 0 || index < info.RotateActuators),
                _ => true,
            };
        }

        // ── Device status bar ─────────────────────────────────────────────────
        private void DrawDeviceStatusBar(float y)
        {
            var prev = GUI.color;
            GUI.color = HapticsTheme.Ink;
            GUI.DrawTexture(new Rect(0, y, WinW, 24f), Texture2D.whiteTexture);
            GUI.color = HapticsTheme.Line;
            GUI.DrawTexture(new Rect(0, y, WinW, 1f), Texture2D.whiteTexture);  // top hairline
            GUI.color = prev;

            if (!ButtplugManager.IsConnected)
            {
                _styleStatus.normal.textColor = HapticsTheme.Danger;
                GUI.Label(new Rect(Pad, y + 4f, WinW - Pad * 2f, 18f),
                          "⚠  Not connected to Intiface Central — start it before launching the game.", _styleStatus);
                return;
            }

            int count = ButtplugManager.DeviceCount;
            if (count == 0)
            {
                _styleStatus.normal.textColor = HapticsTheme.Warn;
                GUI.Label(new Rect(Pad, y + 4f, WinW - Pad * 2f, 18f),
                          "✓ Intiface connected — scanning for devices…", _styleStatus);
                return;
            }

            _styleStatus.normal.textColor = HapticsTheme.Accent;
            var sb = new StringBuilder($"✓ {count} device(s):  ");
            string[] names = ButtplugManager.GetDeviceNames();
            for (int i = 0; i < names.Length; i++)
            {
                var info = ButtplugManager.GetDeviceInfo(i);
                sb.Append($"[#{i} {names[i]} — {info.CapSummary()}]  ");
            }
            GUI.Label(new Rect(Pad, y + 4f, WinW - Pad * 2f, 18f), sb.ToString(), _styleStatus);
        }

        // ── Content height (for settings scroll view) ─────────────────────────
        private float ContentHeight()
        {
            float h = 0f;
            for (int ci = 0; ci < _categories.Length; ci++)
            {
                h += RowH + 2f;
                if (_catOpen[ci]) h += _categories[ci].Rows.Length * (RowH + 2f) + 4f;
            }
            return h;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static EventRow Row(string label, EventConfig cfg) => new EventRow(label, cfg);

        private class CategoryData
        {
            public string     Name;
            public EventRow[] Rows;
            public CategoryData(string n, EventRow[] r) { Name = n; Rows = r; }
        }

        private class EventRow
        {
            public string      Label;
            public EventConfig Cfg;
            public EventRow(string l, EventConfig c) { Label = l; Cfg = c; }
        }
    }
}
