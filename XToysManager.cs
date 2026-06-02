using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HapticsPlugin
{
    /// <summary>
    /// Manages XToys (xtoys.app) webhook integration as a second haptic output,
    /// firing in parallel alongside Intiface/Buttplug for every game event.
    ///
    /// Protocol (XToys Private Webhook, verified against live endpoint June 2026):
    ///   POST https://webhook.xtoys.app/&lt;webhookId&gt;
    ///   Body (JSON): {"action":"setIntensity","intensity":&lt;0-100&gt;}
    ///   (action keyword is camelCase "setIntensity" — matches XToys' webhook scripts.)
    ///   No auth headers for a private webhook. The webhook ID is the only credential.
    ///   HTTP 200 = received. Does NOT confirm the toy actually felt it.
    ///   (The legacy GET xtoys.app/webhook?id=... endpoint now 301-redirects here.)
    ///
    /// Key design decisions (from VSE reference):
    ///   - Single static HttpClient (never new per call — port exhaustion)
    ///   - All sends are async fire-and-forget — never blocks the Unity main thread
    ///   - Deduplicates: skips if the same integer value would be re-sent
    ///   - Always sends intensity=0 after the event duration (devices hold last value)
    ///   - Respects XToysMinDurationMs: pads short events like gun shots (80ms)
    ///     that would arrive after internet latency makes them unfelt
    ///   - New events cancel in-flight decay from the previous event
    ///
    /// IMPORTANT FOR USERS:
    ///   XToys requires the xtoys.app browser tab to remain open with a script
    ///   loaded and the toy connected. See in-game XToys tab for setup guide.
    /// </summary>
    public static class XToysManager
    {
        // Single HttpClient for the plugin lifetime — DO NOT new one per request.
        private static readonly HttpClient _http;
        private const string BaseUrl = "https://webhook.xtoys.app";

        private static string _webhookId         = "";
        private static int    _lastSentIntensity  = -1;   // -1 = nothing sent yet this session
        private static CancellationTokenSource? _decayCts;
        private static readonly object           _decayLock = new object();

        static XToysManager()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        // ── Public surface ─────────────────────────────────────────────────────
        public static bool   IsEnabled  => HapticsConfig.XToysEnabled?.Value == true
                                         && !string.IsNullOrWhiteSpace(_webhookId);
        public static string WebhookId  => _webhookId;

        /// <summary>Apply config — called once on startup and again if the setting changes.</summary>
        public static void Configure(string webhookId)
        {
            _webhookId = webhookId?.Trim() ?? "";
            if (IsEnabled)
                HapticsLogger.Info(LogCat.XToys, "Configured — webhook ID ready.");
            else
                HapticsLogger.Info(LogCat.XToys, "Webhook ID empty or XToys disabled — output off.");
        }

        /// <summary>
        /// Fire an event. Called from EventConfig.FireWithIntensity alongside Buttplug.
        /// intensity: 0.0–1.0.  durationMs: event duration (padded to XToysMinDurationMs if shorter).
        /// </summary>
        public static void Fire(float intensity, int durationMs)
        {
            if (!IsEnabled) return;

            double multiplier     = HapticsConfig.XToysMultiplier?.Value ?? 1.0f;
            int    scaled         = (int)Math.Max(0, Math.Min(100, intensity * multiplier * 100.0));
            int    minDur         = HapticsConfig.XToysMinDurationMs?.Value ?? 300;
            int    effectiveDurMs = System.Math.Max(durationMs, minDur);

            _ = FireAsync(scaled, effectiveDurMs);
        }

        /// <summary>Send a raw 0-100 intensity for a fixed duration — used by the GUI test button.</summary>
        public static Task FireRawAsync(int intensity, int durationMs)
        {
            if (string.IsNullOrWhiteSpace(_webhookId))
            {
                HapticsLogger.Warning(LogCat.XToys, "Test fired but webhook ID is not set.");
                return Task.CompletedTask;
            }
            HapticsLogger.Info(LogCat.XToys, $"Test: {intensity}% for {durationMs}ms…");
            return FireAsync(intensity, durationMs);
        }

        /// <summary>Immediately send intensity=0 and cancel any pending decay. Called on shutdown.</summary>
        public static async Task StopAsync()
        {
            lock (_decayLock) { _decayCts?.Cancel(); }
            await SendIntensityAsync(0);
        }

        // ── Internal ──────────────────────────────────────────────────────────
        private static async Task FireAsync(int intensity, int durationMs)
        {
            // Cancel any in-flight decay so the previous event's stop doesn't
            // cut off this new event.
            CancellationTokenSource cts;
            lock (_decayLock)
            {
                _decayCts?.Cancel();
                _decayCts = new CancellationTokenSource();
                cts = _decayCts;
            }

            await SendIntensityAsync(intensity);

            try
            {
                await Task.Delay(durationMs, cts.Token);
                await SendIntensityAsync(0);
            }
            catch (OperationCanceledException)
            {
                // A newer event superseded this one before we could send the stop.
                // The new event's FireAsync will own the cleanup.
            }
        }

        private static async Task SendIntensityAsync(int intensity)
        {
            intensity = Math.Max(0, Math.Min(100, intensity));

            // Deduplicate — skip if the device is already at this intensity.
            if (intensity == _lastSentIntensity) return;

            // POST the command as JSON to the private-webhook endpoint.
            // Escape the webhook ID in case it contains URL-special characters.
            string url  = $"{BaseUrl}/{Uri.EscapeDataString(_webhookId)}";
            string json = $"{{\"action\":\"setIntensity\",\"intensity\":{intensity}}}";
            try
            {
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                using var resp = await _http.PostAsync(url, content).ConfigureAwait(false);
                _lastSentIntensity = intensity;
                HapticsLogger.Verbose(LogCat.XToys, $"→ {intensity}%  HTTP {(int)resp.StatusCode}");
            }
            catch (TaskCanceledException)
            {
                HapticsLogger.Warning(LogCat.XToys, "Request timed out (10s) — xtoys.app unreachable?");
            }
            catch (Exception ex)
            {
                HapticsLogger.Warning(LogCat.XToys, $"Send failed: {ex.Message}");
            }
        }
    }
}
