using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Mosaic.Bridge.Core.Diagnostics
{
    /// <summary>
    /// Structured log entry captured from the Unity console.
    /// </summary>
    public sealed class ConsoleEntry
    {
        public DateTime Timestamp { get; }
        public LogType LogType { get; }
        public string Message { get; }
        public string StackTrace { get; }

        public ConsoleEntry(DateTime timestamp, LogType logType, string message, string stackTrace)
        {
            Timestamp = timestamp;
            LogType = logType;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
        }
    }

    /// <summary>
    /// Captures Unity console log messages to an in-memory buffer and a disk file.
    /// Thread-safe. Subscribes via logMessageReceivedThreaded for correctness across threads.
    /// Supports domain-reload-safe init/shutdown cycle via EnsureInitialized()/Shutdown().
    /// </summary>
    [InitializeOnLoad]
    public static class ConsoleLogCapture
    {
        private static readonly object _lock = new object();     // guards buffer + _initialized
        private static readonly object _fileLock = new object(); // guards file writes
        private static bool _initialized;
        private static string _logPath;
        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
        private const int BufferCapacity = 1000;

        private static readonly List<ConsoleEntry> _buffer = new List<ConsoleEntry>(BufferCapacity);

        static ConsoleLogCapture()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Subscribes to logMessageReceivedThreaded exactly once. Safe to call multiple times.
        /// </summary>
        public static void EnsureInitialized()
        {
            lock (_lock)
            {
                if (_initialized) return;
                _initialized = true;

                var dir = Path.Combine(Application.persistentDataPath, "MosaicBridge");
                Directory.CreateDirectory(dir);
                _logPath = Path.Combine(dir, "console.log");

                RotateIfNeeded();

                Application.logMessageReceivedThreaded -= OnLogMessage;
                Application.logMessageReceivedThreaded += OnLogMessage;

                WriteFileEntry("=== Mosaic Bridge Session Start ===", "INFO");
            }
        }

        /// <summary>
        /// Unsubscribes and resets state. Call before domain reload to allow clean re-init.
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                Application.logMessageReceivedThreaded -= OnLogMessage;
                _initialized = false;
                _buffer.Clear();
            }
        }

        /// <summary>Returns the absolute path to the console log file.</summary>
        public static string GetLogPath() => _logPath;

        /// <summary>
        /// Returns up to <paramref name="count"/> entries filtered to
        /// <see cref="LogType.Error"/> and <see cref="LogType.Exception"/> only,
        /// in chronological order (oldest first).
        /// </summary>
        public static List<ConsoleEntry> GetLastErrors(int count)
        {
            lock (_lock)
            {
                var result = new List<ConsoleEntry>();
                foreach (var entry in _buffer)
                {
                    if (entry.LogType == LogType.Error || entry.LogType == LogType.Exception)
                        result.Add(entry);
                }

                // Return the last `count` matching entries
                var start = Math.Max(0, result.Count - count);
                return result.GetRange(start, result.Count - start);
            }
        }

#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
        /// <summary>Test helper: inject a pre-built entry directly into the buffer.</summary>
        internal static void InjectForTest(ConsoleEntry entry)
        {
            lock (_lock)
            {
                AddToBuffer(entry);
            }
        }

        /// <summary>Test helper: clear the in-memory buffer.</summary>
        internal static void ClearBuffer()
        {
            lock (_lock) { _buffer.Clear(); }
        }
#endif

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            var entry = new ConsoleEntry(DateTime.UtcNow, type, message, stackTrace);

            lock (_lock)
            {
                AddToBuffer(entry);
            }

            string level = type switch
            {
                LogType.Error     => "ERROR",
                LogType.Assert    => "ASSERT",
                LogType.Warning   => "WARN",
                LogType.Log       => "INFO",
                LogType.Exception => "EXCEPTION",
                _                 => "UNKNOWN",
            };

            string fileMessage = message;
            if (type == LogType.Error || type == LogType.Exception)
            {
                var firstLine = stackTrace?.Split('\n')[0];
                if (!string.IsNullOrEmpty(firstLine))
                    fileMessage += $" | {firstLine.Trim()}";
            }

            WriteFileEntry(fileMessage, level);
        }

        private static void AddToBuffer(ConsoleEntry entry)
        {
            if (_buffer.Count >= BufferCapacity)
                _buffer.RemoveAt(0);
            _buffer.Add(entry);
        }

        private static void WriteFileEntry(string message, string level)
        {
            if (string.IsNullOrEmpty(_logPath)) return;
            lock (_fileLock)
            {
                try
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                    var line = $"[{timestamp}] [{level}] {message}\n";
                    File.AppendAllText(_logPath, line);
                }
                catch
                {
                    // Silently ignore write failures (disk full, permissions, etc.)
                }
            }
        }

        private static void RotateIfNeeded()
        {
            if (string.IsNullOrEmpty(_logPath)) return;
            try
            {
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxFileSize)
                {
                    var backupPath = _logPath + ".old";
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Move(_logPath, backupPath);
                }
            }
            catch
            {
                // Best-effort rotation — never crash on file operations
            }
        }
    }
}
