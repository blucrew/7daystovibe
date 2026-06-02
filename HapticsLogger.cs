using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace HapticsPlugin
{
    // ── Log verbosity levels ──────────────────────────────────────────────────
    public enum LogVerbosity { Off = 0, Error = 1, Warning = 2, Info = 3, Verbose = 4 }

    // ── Log categories ────────────────────────────────────────────────────────
    public static class LogCat
    {
        public const string System  = "System ";
        public const string Device  = "Device ";
        public const string Buttplug= "Buttplug";
        public const string XToys   = "XToys  ";
        public const string Event   = "Event  ";
        public const string Patch   = "Patch  ";
    }

    // ── A single log entry ────────────────────────────────────────────────────
    public struct LogEntry
    {
        public DateTime     Time;
        public LogVerbosity Level;
        public string       Category;
        public string       Message;
    }

    /// <summary>
    /// Centralised logger for the haptics plugin.
    ///
    /// Features
    /// ────────
    /// • Writes to the BepInEx log (always).
    /// • Keeps a circular in-memory ring buffer (300 entries) for the in-game overlay.
    /// • Optionally writes a timestamped file to BepInEx/logs/.
    ///
    /// Call Init() from Plugin.Awake() before touching anything else.
    /// All public methods are thread-safe (the lock covers both the buffer and the file).
    /// </summary>
    public static class HapticsLogger
    {
        // ── Config ────────────────────────────────────────────────────────────
        public static ConfigEntry<LogVerbosity> Verbosity   = null!;
        public static ConfigEntry<bool>          WriteToFile = null!;

        // ── Internal state ────────────────────────────────────────────────────
        private static ManualLogSource?  _bepLog;
        private static StreamWriter?     _fileWriter;

        private const  int               BufferCapacity = 300;
        private static readonly LogEntry[] _ring        = new LogEntry[BufferCapacity];
        private static volatile int      _head;        // next write index (mod BufferCapacity)
        private static volatile int      _count;       // total entries ever written (not capped)
        private static readonly object   _lock         = new object();

        // ── Init ──────────────────────────────────────────────────────────────
        public static void Init(ManualLogSource bepLog, ConfigFile cfg)
        {
            _bepLog = bepLog;

            Verbosity = cfg.Bind("Logging", "Verbosity", LogVerbosity.Info,
                "How much detail to log. Off=nothing, Error=errors only, Warning=+warnings, " +
                "Info=events+devices, Verbose=every command sent to Intiface. " +
                "Valid values: Off, Error, Warning, Info, Verbose.");

            WriteToFile = cfg.Bind("Logging", "WriteToFile", false,
                "Write a timestamped haptics log file to BepInEx/logs/. Off by default " +
                "to avoid disk spam — enable for debugging sessions only.");

            if (WriteToFile.Value) OpenFileWriter();

            WriteToFile.SettingChanged += (_, _) =>
            {
                if (WriteToFile.Value) OpenFileWriter();
                else                   CloseFileWriter();
            };

            Info(LogCat.System, $"HapticsLogger initialised — verbosity={Verbosity.Value}, file={WriteToFile.Value}");
        }

        // ── Public log methods ────────────────────────────────────────────────
        public static void Verbose(string cat, string msg) => Write(LogVerbosity.Verbose, cat, msg);
        public static void Info   (string cat, string msg) => Write(LogVerbosity.Info,    cat, msg);
        public static void Warning(string cat, string msg) => Write(LogVerbosity.Warning, cat, msg);
        public static void Error  (string cat, string msg) => Write(LogVerbosity.Error,   cat, msg);

        // ── Buffer read ───────────────────────────────────────────────────────
        /// <summary>Returns a snapshot of the ring buffer in chronological order.</summary>
        public static LogEntry[] GetSnapshot()
        {
            lock (_lock)
            {
                int total  = Math.Min(_count, BufferCapacity);
                var result = new LogEntry[total];
                // Oldest entry is at (_head - total + BufferCapacity) % BufferCapacity
                int start = (_head - total + BufferCapacity) % BufferCapacity;
                for (int i = 0; i < total; i++)
                    result[i] = _ring[(start + i) % BufferCapacity];
                return result;
            }
        }

        /// <summary>Total number of entries ever written (use to detect new entries).</summary>
        public static int TotalCount => _count;

        public static void Clear()
        {
            lock (_lock) { _head = 0; _count = 0; }
            Info(LogCat.System, "Log buffer cleared.");
        }

        public static void Shutdown()
        {
            Info(LogCat.System, "HapticsLogger shutting down.");
            CloseFileWriter();
        }

        // ── Internal ──────────────────────────────────────────────────────────
        private static void Write(LogVerbosity level, string category, string message)
        {
            // Fast-exit before taking the lock
            if (Verbosity == null || level > Verbosity.Value) return;

            var entry = new LogEntry
            {
                Time     = DateTime.Now,
                Level    = level,
                Category = category,
                Message  = message,
            };

            lock (_lock)
            {
                _ring[_head] = entry;
                _head        = (_head + 1) % BufferCapacity;
                _count++;

                _fileWriter?.WriteLine(FormatFile(entry));
            }

            // BepInEx log — outside the lock, fire-and-forget is fine
            string line = $"[{category.Trim()}] {message}";
            switch (level)
            {
                case LogVerbosity.Verbose: _bepLog?.LogDebug(line);   break;
                case LogVerbosity.Info:    _bepLog?.LogInfo(line);    break;
                case LogVerbosity.Warning: _bepLog?.LogWarning(line); break;
                case LogVerbosity.Error:   _bepLog?.LogError(line);   break;
            }
        }

        private static string FormatFile(in LogEntry e)
            => $"{e.Time:HH:mm:ss.fff}  {LevelTag(e.Level)}  [{e.Category}]  {e.Message}";

        private static string LevelTag(LogVerbosity v) => v switch
        {
            LogVerbosity.Verbose => "[VERBOSE]",
            LogVerbosity.Info    => "[INFO   ]",
            LogVerbosity.Warning => "[WARN   ]",
            LogVerbosity.Error   => "[ERROR  ]",
            _                    => "[?      ]",
        };

        private static void OpenFileWriter()
        {
            CloseFileWriter();
            try
            {
                string dir  = Path.Combine(Paths.BepInExRootPath, "logs");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"7dtd_haptics_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
                _fileWriter = new StreamWriter(path, append: false) { AutoFlush = true };
                _fileWriter.WriteLine($"# 7DTD Haptics debug log — opened {DateTime.Now:O}");
                _fileWriter.WriteLine($"# Game version / BepInEx version recorded in BepInEx/LogOutput.log");
                _fileWriter.WriteLine();
                Info(LogCat.System, $"Log file opened: {path}");
            }
            catch (Exception ex)
            {
                _bepLog?.LogWarning($"[Haptics] Could not open log file: {ex.Message}");
            }
        }

        private static void CloseFileWriter()
        {
            try { _fileWriter?.Flush(); _fileWriter?.Close(); } catch { }
            _fileWriter = null;
        }
    }
}
