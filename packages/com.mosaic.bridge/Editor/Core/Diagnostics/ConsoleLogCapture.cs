using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Mosaic.Bridge.Core.Diagnostics
{
    /// <summary>
    /// Captures ALL Unity console log messages (Log, Warning, Error, Exception, Assert)
    /// to a timestamped file for E2E test correlation.
    /// Registers on domain reload via [InitializeOnLoad].
    /// Thread-safe: all writes are serialized via lock.
    /// </summary>
    [InitializeOnLoad]
    public static class ConsoleLogCapture
    {
        private static readonly object _lock = new object();
        private static string _logPath;
        private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

        static ConsoleLogCapture()
        {
            var dir = Path.Combine(Application.persistentDataPath, "MosaicBridge");
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "console.log");

            RotateIfNeeded();

            Application.logMessageReceived -= OnLogMessage;
            Application.logMessageReceived += OnLogMessage;

            WriteEntry("=== Mosaic Bridge Session Start ===", "INFO");
        }

        /// <summary>Returns the absolute path to the console log file.</summary>
        public static string GetLogPath() => _logPath;

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            string level = type switch
            {
                LogType.Error     => "ERROR",
                LogType.Assert    => "ASSERT",
                LogType.Warning   => "WARN",
                LogType.Log       => "INFO",
                LogType.Exception => "EXCEPTION",
                _                 => "UNKNOWN",
            };

            string entry = message;
            if (type == LogType.Error || type == LogType.Exception)
            {
                var firstLine = stackTrace?.Split('\n')[0];
                if (!string.IsNullOrEmpty(firstLine))
                    entry += $" | {firstLine.Trim()}";
            }

            WriteEntry(entry, level);
        }

        private static void WriteEntry(string message, string level)
        {
            lock (_lock)
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
