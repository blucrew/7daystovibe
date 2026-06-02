using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Client;
using Buttplug.Client.Connectors.WebsocketConnector;

namespace HapticsPlugin
{
    // ── Device capability snapshot (immutable, thread-safe to read) ───────────
    public struct DeviceInfo
    {
        public string Name;
        public int    VibrateMotors;
        public int    LinearActuators;
        public int    RotateActuators;

        public bool HasVibrate => VibrateMotors   > 0;
        public bool HasLinear  => LinearActuators > 0;
        public bool HasRotate  => RotateActuators > 0;
        public bool IsEmpty    => !HasVibrate && !HasLinear && !HasRotate;

        public string CapSummary()
        {
            var parts = new List<string>(3);
            if (HasVibrate) parts.Add($"Vib×{VibrateMotors}");
            if (HasLinear)  parts.Add($"Lin×{LinearActuators}");
            if (HasRotate)  parts.Add($"Rot×{RotateActuators}");
            return parts.Count > 0 ? string.Join(" ", parts) : "—";
        }
    }

    /// <summary>
    /// Manages the Buttplug/Intiface connection and multi-device, multi-actuator haptic dispatch.
    ///
    /// Threading model
    /// ───────────────
    /// • All public methods are safe to call from the Unity main thread.
    /// • ButtplugClient callbacks arrive on background I/O threads.
    /// • _devices and _infos are protected by _devLock.
    /// • _connected is volatile to avoid stale-read races between I/O and main threads.
    ///
    /// Routing model
    /// ─────────────
    /// • deviceIndex  -1  = all connected devices
    ///                0…N = specific slot (connection order)
    /// • actuatorType All     = every actuator the device has
    ///                Vibrate / Linear / Rotate = only that class
    /// • actuatorIndex -1 = all of the chosen type, 0…N = specific actuator
    ///
    /// Cancellation
    /// ────────────
    /// Each EventConfig owns a CancellationTokenSource (via FireToken).
    /// Calling Fire() cancels any still-running task for that event before
    /// starting the new one, so rapid-fire events don't pile up motor commands.
    /// </summary>
    public static class ButtplugManager
    {
        // ── Private state ─────────────────────────────────────────────────────
        private static ButtplugClient? _client;
        private static volatile bool   _connected;   // volatile: written on I/O thread, read on main thread
        private static int             _reconnectAttempts;

        // ── Named constants ───────────────────────────────────────────────────
        private const int    MaxReconnectAttempts = 5;
        private const string ServerUri            = "ws://127.0.0.1:12345";
        private const int    RampSteps            = 10;    // steps in vibrate ramp-up/down
        private const int    RetractMs            = 200;   // ms to return linear actuator to home on exit
        private const int    MinStrokeHalfMs      = 50;    // minimum half-stroke for linear pulse (ms)
        private const int    StrokeMsPerIntensity = 300;   // half-stroke duration at intensity 1.0

        // Ordered list of connected devices (connection order = slot index).
        // Capabilities are snapshotted at connection time so we never need to
        // call device.VibrateAttributes inside the lock (avoids potential
        // attribute-enumeration races with the Buttplug I/O thread).
        private static readonly List<ButtplugClientDevice> _devices = new List<ButtplugClientDevice>();
        private static readonly List<DeviceInfo>           _infos   = new List<DeviceInfo>();
        private static readonly object                     _devLock = new object();

        // ── Public read-only surface ──────────────────────────────────────────
        public static bool IsConnected => _connected;

        // Incremented every time a device is added or removed.
        // The GUI polls this to know when to rebuild its actuator option cache.
        // Private field + public read-only property prevents external mutation.
        private static volatile int _deviceListVersion;
        public  static int DeviceListVersion => _deviceListVersion;

        public static int DeviceCount
        {
            get { lock (_devLock) { return _devices.Count; } }
        }

        public static string GetDeviceName(int index)
        {
            if (index < 0) return "All Devices";
            lock (_devLock)
                return index < _infos.Count ? _infos[index].Name : $"Slot {index} (none)";
        }

        public static string[] GetDeviceNames()
        {
            lock (_devLock) return _infos.Select(i => i.Name).ToArray();
        }

        /// <summary>
        /// Returns a DeviceInfo snapshot for a slot.
        /// index -1 returns a merged view of all connected devices.
        /// </summary>
        public static DeviceInfo GetDeviceInfo(int index)
        {
            lock (_devLock)
            {
                if (index < 0)
                    return new DeviceInfo
                    {
                        Name             = "All Devices",
                        VibrateMotors    = _infos.Sum(i => i.VibrateMotors),
                        LinearActuators  = _infos.Sum(i => i.LinearActuators),
                        RotateActuators  = _infos.Sum(i => i.RotateActuators),
                    };

                if (index < _infos.Count) return _infos[index];
                return new DeviceInfo { Name = $"Slot {index} (none)" };
            }
        }

        // ── Init / Shutdown ───────────────────────────────────────────────────
        public static async Task InitAsync()
        {
            _reconnectAttempts = 0;
            HapticsLogger.Info(LogCat.Device, $"Connecting to Intiface Central at {ServerUri}…");
            await ConnectAsync();
        }

        private static async Task ConnectAsync()
        {
            // Guard against double-init
            if (_client != null)
            {
                try { await _client.DisconnectAsync(); } catch { }
                _client = null;
            }

            _client = new ButtplugClient("7DTD Haptics");

            _client.DeviceAdded += (_, args) =>
            {
                var dev  = args.Device;
                // Snapshot capabilities now, on the I/O thread, before entering the lock.
                // This avoids calling LINQ on device attributes while holding _devLock.
                var info = new DeviceInfo
                {
                    Name             = dev.Name,
                    VibrateMotors    = dev.VibrateAttributes.Count(),
                    LinearActuators  = dev.LinearAttributes .Count(),
                    RotateActuators  = dev.RotateAttributes .Count(),
                };
                int slot;
                lock (_devLock)
                {
                    slot = _devices.Count;
                    _devices.Add(dev);
                    _infos.Add(info);
                }
                Interlocked.Increment(ref _deviceListVersion);
                HapticsLogger.Info(LogCat.Device, $"Connected [slot {slot}]: {dev.Name}  ({info.CapSummary()})");
            };

            _client.DeviceRemoved += (_, args) =>
            {
                lock (_devLock)
                {
                    int idx = _devices.IndexOf(args.Device);
                    if (idx >= 0) { _devices.RemoveAt(idx); _infos.RemoveAt(idx); }
                }
                Interlocked.Increment(ref _deviceListVersion);
                HapticsLogger.Info(LogCat.Device, $"Removed: {args.Device.Name}");
            };

            _client.ServerDisconnect += (_, _) =>
            {
                _connected = false;
                HapticsLogger.Warning(LogCat.Device, "Intiface disconnected — attempting reconnect…");
                _ = TryReconnectAsync();
            };

            try
            {
                var connector = new ButtplugWebsocketConnector(new Uri(ServerUri));
                await _client.ConnectAsync(connector);
                _connected = true;
                _reconnectAttempts = 0;
                HapticsLogger.Info(LogCat.Device, "Connected to Intiface Central!");
                await _client.StartScanningAsync();
                HapticsLogger.Info(LogCat.Device, "Scanning for devices…");
            }
            catch (Exception ex)
            {
                HapticsLogger.Error(LogCat.Device, $"Could not connect to Intiface Central: {ex.Message}");
                HapticsLogger.Error(LogCat.Device, "Make sure Intiface Central is running before starting the game.");
            }
        }

        private static async Task TryReconnectAsync()
        {
            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                HapticsLogger.Error(LogCat.Device, $"Gave up reconnecting after {MaxReconnectAttempts} attempts.");
                return;
            }
            _reconnectAttempts++;
            int delaySeconds = Math.Min(5 * _reconnectAttempts, 30); // 5s, 10s, 15s, 20s, 25s
            HapticsLogger.Info(LogCat.Device,
                $"Reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts} in {delaySeconds}s…");
            await Task.Delay(delaySeconds * 1000);
            // Clear old device list before reconnecting
            lock (_devLock) { _devices.Clear(); _infos.Clear(); }
            await ConnectAsync();
        }

        public static async Task ShutdownAsync()
        {
            if (_client == null || !_connected) return;
            HapticsLogger.Info(LogCat.Device, "Shutting down — stopping all devices…");
            ButtplugClientDevice[] snap;
            lock (_devLock) snap = _devices.ToArray();
            foreach (var d in snap) try { await d.Stop(); } catch { }
            try { await _client.DisconnectAsync(); } catch { }
        }

        // ── Fire entry point ──────────────────────────────────────────────────
        /// <summary>
        /// Fire haptics for one event.  cancelToken is owned by the EventConfig and
        /// allows the previous task to be cancelled when the same event fires again rapidly.
        /// </summary>
        public static void Fire(float intensity, int durationMs, HapticPattern pattern,
                                int deviceIndex, HapticActuatorType actuatorType, int actuatorIndex,
                                ref CancellationTokenSource? cts)
        {
            // Cancel the previous task for this event slot
            cts?.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            var targets = GetTargets(deviceIndex);
            if (targets.Length == 0) return;

            double clamped = Math.Max(0.0, Math.Min(1.0, intensity));
            HapticsLogger.Verbose(LogCat.Buttplug,
                $"→ {targets.Length} device(s) | intensity={clamped:F2} dur={durationMs}ms " +
                $"pattern={pattern} type={actuatorType}[{actuatorIndex}]");

            Task.Run(async () =>
            {
                // Fire all target devices in parallel; isolate each device's errors
                var tasks = targets.Select(async d =>
                {
                    try   { await FireDevice(d, clamped, durationMs, pattern, actuatorType, actuatorIndex, token); }
                    catch (OperationCanceledException) { /* expected on rapid re-fire */ }
                    catch (Exception ex)
                    {
                        HapticsLogger.Error(LogCat.Device, $"Device '{d.Name}' error: {ex.Message}");
                    }
                });
                await Task.WhenAll(tasks);
            }, token);
        }

        // ── Legacy convenience wrappers (used internally and by GUI Test button) ──
        public static void Vibrate(float intensity, int durationMs = 300, int deviceIndex = -1)
        {
            CancellationTokenSource? dummy = null;
            Fire(intensity, durationMs, HapticPattern.Vibrate, deviceIndex, HapticActuatorType.Vibrate, -1, ref dummy);
        }

        public static void Pulse(float peakIntensity, int durationMs = 500, int deviceIndex = -1)
        {
            CancellationTokenSource? dummy = null;
            Fire(peakIntensity, durationMs, HapticPattern.Pulse, deviceIndex, HapticActuatorType.Vibrate, -1, ref dummy);
        }

        // ── Per-device dispatcher ─────────────────────────────────────────────
        private static Task FireDevice(ButtplugClientDevice device, double intensity, int durationMs,
                                       HapticPattern pattern, HapticActuatorType type, int actuatorIdx,
                                       CancellationToken token)
        {
            IEnumerable<HapticActuatorType> types;
            if (type == HapticActuatorType.All)
            {
                // Only fire types the device actually supports
                var list = new List<HapticActuatorType>(3);
                if (device.VibrateAttributes.Any()) list.Add(HapticActuatorType.Vibrate);
                if (device.LinearAttributes .Any()) list.Add(HapticActuatorType.Linear);
                if (device.RotateAttributes .Any()) list.Add(HapticActuatorType.Rotate);
                types = list;
            }
            else
            {
                types = new[] { type };
            }

            return Task.WhenAll(types.Select(t => t switch
            {
                HapticActuatorType.Vibrate => FireVibrate(device, intensity, durationMs, pattern, actuatorIdx, token),
                HapticActuatorType.Linear  => FireLinear (device, intensity, durationMs, pattern, actuatorIdx, token),
                HapticActuatorType.Rotate  => FireRotate (device, intensity, durationMs,           actuatorIdx, token),
                _ => Task.CompletedTask,
            }));
        }

        // ── Vibrate ───────────────────────────────────────────────────────────
        private static async Task FireVibrate(ButtplugClientDevice device, double intensity,
                                              int durationMs, HapticPattern pattern, int motorIdx,
                                              CancellationToken token)
        {
            var attrs = device.VibrateAttributes.ToList();
            if (attrs.Count == 0) return;

            (uint Index, double Speed)[] Cmd(double speed)
            {
                if (motorIdx < 0)
                    return attrs.Select((_, i) => ((uint)i, speed)).ToArray();
                return motorIdx < attrs.Count
                    ? new[] { ((uint)motorIdx, speed) }
                    : Array.Empty<(uint, double)>();
            }

            try
            {
                if (pattern == HapticPattern.Vibrate)
                {
                    HapticsLogger.Verbose(LogCat.Buttplug,
                        $"  Vibrate {device.Name} speed={intensity:F2} dur={durationMs}ms");
                    await device.VibrateAsync(Cmd(intensity));
                    await Task.Delay(durationMs, token);
                }
                else
                {
                    int stepMs = Math.Max(1, durationMs / (RampSteps * 2));
                    HapticsLogger.Verbose(LogCat.Buttplug,
                        $"  Pulse   {device.Name} peak={intensity:F2} stepMs={stepMs}");
                    for (int i = 0; i <= RampSteps; i++)
                    { await device.VibrateAsync(Cmd(intensity * i / RampSteps)); await Task.Delay(stepMs, token); }
                    for (int i = RampSteps; i >= 0; i--)
                    { await device.VibrateAsync(Cmd(intensity * i / RampSteps)); await Task.Delay(stepMs, token); }
                }
            }
            finally
            {
                // Always attempt to stop, even if cancelled or if an error occurred mid-vibration.
                try { device.Stop(); } catch { }
            }
        }

        // ── Linear / thrust ───────────────────────────────────────────────────
        private static async Task FireLinear(ButtplugClientDevice device, double intensity,
                                             int durationMs, HapticPattern pattern, int actuatorIdx,
                                             CancellationToken token)
        {
            // intensity=0 would still physically move the actuator to position 0.0.
            // For vibrate/rotate that's a silent stop, but for linear it sends real movement commands.
            if (intensity <= 0) return;

            var attrs = device.LinearAttributes.ToList();
            if (attrs.Count == 0) return;

            // position is normalized 0–1 depth; intensity drives target depth.
            // Buttplug 3.x LinearAsync: no Index in the per-command tuple — entries map to
            // actuators in order. Targeting a specific actuator: build a list of (Duration, Position)
            // entries, with 0-intensity moves for actuators before the target index.
            (uint Duration, double Position)[] Cmd(uint moveMs, double pos)
            {
                if (actuatorIdx < 0)
                    return attrs.Select(_ => (moveMs, pos)).ToArray();
                if (actuatorIdx < attrs.Count)
                {
                    var cmds = new (uint, double)[attrs.Count];
                    for (int i = 0; i < attrs.Count; i++)
                        cmds[i] = i == actuatorIdx ? (moveMs, pos) : (moveMs, 0.0);
                    return cmds;
                }
                return Array.Empty<(uint, double)>();
            }

            try
            {
                if (pattern == HapticPattern.Vibrate)
                {
                    // Single thrust: extend to intensity depth, hold briefly, retract
                    int moveMs = Math.Max(MinStrokeHalfMs, durationMs / 3);
                    HapticsLogger.Verbose(LogCat.Buttplug,
                        $"  Linear  {device.Name} thrust pos={intensity:F2} moveMs={moveMs}");
                    await device.LinearAsync(Cmd((uint)moveMs, intensity));
                    await Task.Delay(moveMs + Math.Max(0, durationMs / 3), token);
                    await device.LinearAsync(Cmd((uint)moveMs, 0.0));
                    await Task.Delay(moveMs, token);
                }
                else
                {
                    // Pulse: oscillating strokes; stroke speed scales with intensity
                    int halfMs    = Math.Max(MinStrokeHalfMs, (int)(StrokeMsPerIntensity * intensity));
                    int fullCycle = halfMs * 2;
                    int strokes   = Math.Max(1, durationMs / fullCycle);
                    HapticsLogger.Verbose(LogCat.Buttplug,
                        $"  Linear  {device.Name} oscillate halfMs={halfMs} strokes={strokes}");
                    for (int s = 0; s < strokes; s++)
                    {
                        await device.LinearAsync(Cmd((uint)halfMs, intensity));
                        await Task.Delay(halfMs, token);
                        await device.LinearAsync(Cmd((uint)halfMs, 0.0));
                        await Task.Delay(halfMs, token);
                    }
                }
            }
            catch (OperationCanceledException) { throw; } // let caller handle
            finally
            {
                // Return to home position (RetractMs delay is intentionally not cancellable)
                try
                {
                    await device.LinearAsync(Cmd((uint)RetractMs, 0.0));
                    await Task.Delay(RetractMs);
                }
                catch { }
            }
        }

        // ── Rotate ────────────────────────────────────────────────────────────
        private static async Task FireRotate(ButtplugClientDevice device, double intensity,
                                             int durationMs, int actuatorIdx, CancellationToken token)
        {
            var attrs = device.RotateAttributes.ToList();
            if (attrs.Count == 0) return;

            // Buttplug 3.x RotateAsync: no Index in the per-command tuple.
            (double Speed, bool Clockwise)[] Cmd(double speed)
            {
                if (actuatorIdx < 0)
                    return attrs.Select(_ => (speed, true)).ToArray();
                if (actuatorIdx < attrs.Count)
                {
                    var cmds = new (double, bool)[attrs.Count];
                    for (int i = 0; i < attrs.Count; i++)
                        cmds[i] = i == actuatorIdx ? (speed, true) : (0.0, true);
                    return cmds;
                }
                return Array.Empty<(double, bool)>();
            }

            try
            {
                HapticsLogger.Verbose(LogCat.Buttplug,
                    $"  Rotate  {device.Name} speed={intensity:F2} dur={durationMs}ms");
                await device.RotateAsync(Cmd(intensity));
                await Task.Delay(durationMs, token);
            }
            finally
            {
                try { device.Stop(); } catch { }
            }
        }

        // ── Target resolution ─────────────────────────────────────────────────
        private static ButtplugClientDevice[] GetTargets(int deviceIndex)
        {
            if (!_connected || _client == null) return Array.Empty<ButtplugClientDevice>();
            lock (_devLock)
            {
                if (_devices.Count == 0) return Array.Empty<ButtplugClientDevice>();
                if (deviceIndex < 0)                     return _devices.ToArray();
                if (deviceIndex < _devices.Count)        return new[] { _devices[deviceIndex] };
                return Array.Empty<ButtplugClientDevice>();
            }
        }
    }
}
