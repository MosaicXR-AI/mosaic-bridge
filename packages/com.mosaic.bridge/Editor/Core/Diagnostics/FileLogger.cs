using System;
using System.Collections.Generic;
using System.IO;
using Mosaic.Bridge.Contracts.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Core.Diagnostics
{
    /// <summary>
    /// Writes structured JSON log entries to the runtime directory.
    /// Each entry is a single JSON line (JSONL format) for easy parsing.
    /// Rotates files daily, keeps last 7 days.
    /// Thread-safe: all writes are serialized via lock.
    /// </summary>
    public sealed class FileLogger : IMosaicLogger, IDisposable
    {
        private readonly string _logDirectory;
        private readonly object _lock = new object();
        private readonly int _retentionDays;
        private StreamWriter _writer;
        private string _currentDate;
        private bool _disposed;

        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        public FileLogger(string runtimeDirectory, int retentionDays = 7)
        {
            _retentionDays = retentionDays;
            _logDirectory = Path.Combine(runtimeDirectory, "logs");
            Directory.CreateDirectory(_logDirectory);
            OpenLogFile();
            CleanupOldLogs();
        }

        public bool IsEnabled(LogLevel level) => level >= MinimumLevel;

        public void Trace(string message, params (string Key, object Value)[] context)
        {
            if (!IsEnabled(LogLevel.Trace)) return;
            WriteEntry("TRACE", message, context);
        }

        public void Debug(string message, params (string Key, object Value)[] context)
        {
            if (!IsEnabled(LogLevel.Debug)) return;
            WriteEntry("DEBUG", message, context);
        }

        public void Info(string message, params (string Key, object Value)[] context)
        {
            if (!IsEnabled(LogLevel.Info)) return;
            WriteEntry("INFO", message, context);
        }

        public void Warn(string message, params (string Key, object Value)[] context)
        {
            if (!IsEnabled(LogLevel.Warn)) return;
            WriteEntry("WARN", message, context);
        }

        public void Error(string message, Exception exception = null, params (string Key, object Value)[] context)
        {
            if (!IsEnabled(LogLevel.Error)) return;
            WriteEntry("ERROR", message, context, exception);
        }

        /// <summary>
        /// Returns the last N log lines from the current day's file.
        /// Used by the diagnostics/export MCP tool.
        /// </summary>
        public List<string> ReadLastEntries(int count = 100)
        {
            lock (_lock)
            {
                var result = new List<string>();
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var filePath = GetLogFilePath(today);

                if (!File.Exists(filePath))
                    return result;

                // Flush before reading so we get the latest entries
                _writer?.Flush();

                var allLines = File.ReadAllLines(filePath);
                var start = Math.Max(0, allLines.Length - count);
                for (int i = start; i < allLines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(allLines[i]))
                        result.Add(allLines[i]);
                }

                return result;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
        }

        private void WriteEntry(string level, string message, (string Key, object Value)[] context, Exception exception = null)
        {
            lock (_lock)
            {
                if (_disposed) return;

                RotateIfNeeded();

                try
                {
                    var obj = new JObject
                    {
                        ["ts"] = DateTime.UtcNow.ToString("o"),
                        ["level"] = level,
                        ["msg"] = message
                    };

                    if (context != null)
                    {
                        foreach (var (key, value) in context)
                        {
                            obj[key] = value != null ? JToken.FromObject(value) : JValue.CreateNull();
                        }
                    }

                    if (exception != null)
                    {
                        obj["exception"] = exception.ToString();
                    }

                    _writer.WriteLine(obj.ToString(Formatting.None));
                }
                catch
                {
                    // Best-effort: never let file logging crash the host
                }
            }
        }

        private void OpenLogFile()
        {
            _currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var filePath = GetLogFilePath(_currentDate);

            _writer = new StreamWriter(filePath, append: true, encoding: System.Text.Encoding.UTF8)
            {
                AutoFlush = true
            };
        }

        private void RotateIfNeeded()
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (today == _currentDate) return;

            _writer?.Flush();
            _writer?.Dispose();
            OpenLogFile();
        }

        private void CleanupOldLogs()
        {
            try
            {
                var cutoff = DateTime.UtcNow.Date.AddDays(-_retentionDays);
                var files = Directory.GetFiles(_logDirectory, "mosaic-bridge-*.jsonl");

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // Format: mosaic-bridge-2026-04-12
                    var datePart = fileName.Length > 14 ? fileName.Substring(14) : null;
                    if (datePart != null && DateTime.TryParse(datePart, out var fileDate))
                    {
                        if (fileDate < cutoff)
                        {
                            File.Delete(file);
                        }
                    }
                }
            }
            catch
            {
                // Best-effort cleanup — never crash on old log deletion
            }
        }

        private string GetLogFilePath(string date)
        {
            return Path.Combine(_logDirectory, $"mosaic-bridge-{date}.jsonl");
        }
    }
}
