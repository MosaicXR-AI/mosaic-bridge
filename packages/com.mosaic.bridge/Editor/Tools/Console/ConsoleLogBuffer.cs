using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mosaic.Bridge.Tools.ConsoleTools
{
    /// <summary>
    /// Captures Unity console log entries via Application.logMessageReceivedThreaded.
    /// Uses a thread-safe ring buffer. Initialized once via [InitializeOnLoad] or manual call.
    /// </summary>
    public static class ConsoleLogBuffer
    {
        private static readonly object _lock = new object();
        private static readonly List<ConsoleEntry> _entries = new List<ConsoleEntry>();
        private const int MaxEntries = 500;
        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            Application.logMessageReceivedThreaded += OnLogMessage;
        }

        public static void Shutdown()
        {
            Application.logMessageReceivedThreaded -= OnLogMessage;
            _initialized = false;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType logType)
        {
            var entry = new ConsoleEntry
            {
                Message = message,
                Level = LogTypeToLevel(logType),
                File = ExtractFile(stackTrace),
                Line = ExtractLine(stackTrace)
            };

            lock (_lock)
            {
                if (_entries.Count >= MaxEntries)
                    _entries.RemoveAt(0);
                _entries.Add(entry);
            }
        }

        /// <summary>
        /// Returns filtered entries from the buffer.
        /// </summary>
        public static List<ConsoleEntry> GetEntries(bool includeInfo, bool includeWarnings, bool includeErrors, int maxResults)
        {
            var result = new List<ConsoleEntry>();
            lock (_lock)
            {
                // Iterate newest first
                for (int i = _entries.Count - 1; i >= 0 && result.Count < maxResults; i--)
                {
                    var e = _entries[i];
                    bool include = false;
                    switch (e.Level)
                    {
                        case "Info": include = includeInfo; break;
                        case "Warning": include = includeWarnings; break;
                        case "Error":
                        case "Exception":
                        case "Assert":
                            include = includeErrors; break;
                    }
                    if (include) result.Add(e);
                }
            }
            result.Reverse(); // Return in chronological order
            return result;
        }

        public static int TotalCount
        {
            get { lock (_lock) { return _entries.Count; } }
        }

        public static void Clear()
        {
            lock (_lock) { _entries.Clear(); }
        }

        private static string LogTypeToLevel(LogType t)
        {
            switch (t)
            {
                case LogType.Error: return "Error";
                case LogType.Exception: return "Exception";
                case LogType.Assert: return "Assert";
                case LogType.Warning: return "Warning";
                case LogType.Log: return "Info";
                default: return "Info";
            }
        }

        private static string ExtractFile(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return "";
            // First line of stack often has "(at path:line)"
            int atIdx = stackTrace.IndexOf("(at ", StringComparison.Ordinal);
            if (atIdx < 0) return "";
            int colonIdx = stackTrace.IndexOf(':', atIdx + 4);
            if (colonIdx < 0) return "";
            return stackTrace.Substring(atIdx + 4, colonIdx - atIdx - 4);
        }

        private static int ExtractLine(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return 0;
            int atIdx = stackTrace.IndexOf("(at ", StringComparison.Ordinal);
            if (atIdx < 0) return 0;
            int colonIdx = stackTrace.IndexOf(':', atIdx + 4);
            if (colonIdx < 0) return 0;
            int endIdx = stackTrace.IndexOf(')', colonIdx);
            if (endIdx < 0) return 0;
            string lineStr = stackTrace.Substring(colonIdx + 1, endIdx - colonIdx - 1);
            return int.TryParse(lineStr, out int line) ? line : 0;
        }
    }
}
