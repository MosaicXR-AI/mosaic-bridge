using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.Tools.ConsoleTools
{
    /// <summary>
    /// Captures Unity console log entries from three sources, in priority order:
    ///   1. Persistent file  — Library/MosaicBridge/console.log — survives both
    ///      console Clear and domain reloads, so nothing is ever lost.
    ///   2. Unity internal LogEntries API — current console window state
    ///      (retroactively captures messages logged before our subscription).
    ///   3. Live ring buffer — in-memory, captured via logMessageReceivedThreaded.
    ///
    /// [InitializeOnLoad] ensures the listener registers at domain reload, not just
    /// on the first tool call, so assembly-guard warnings and other early messages
    /// are captured even before any MCP tool is invoked.
    /// </summary>
    [InitializeOnLoad]
    public static class ConsoleLogBuffer
    {
        // ── Persistent file ───────────────────────────────────────────────────

        private static readonly string s_PersistDir;
        private static readonly string s_PersistPath;
        private const int MaxFileSizeBytes = 256 * 1024; // 256 KB → trim on next init
        private const int TrimToLines      = 1000;

        private static StreamWriter s_Writer;

        // ── In-memory ring buffer ─────────────────────────────────────────────

        private static readonly object         s_Lock    = new object();
        private static readonly List<ConsoleEntry> s_Ring = new List<ConsoleEntry>();
        private const int MaxRingEntries = 500;

        private static bool _initialized;

        // ── Reflection handles for Unity's internal console store ─────────────

        private static Type      _logEntriesType;
        private static Type      _logEntryType;
        private static MethodInfo _startGetting;
        private static MethodInfo _endGetting;
        private static MethodInfo _getEntryInternal;
        private static PropertyInfo _countProp;
        private static FieldInfo _messageField;
        private static FieldInfo _modeField;
        private static bool _reflectionResolved;

        // ── [InitializeOnLoad] static constructor ─────────────────────────────

        static ConsoleLogBuffer()
        {
            // Compute paths once (Application.dataPath available at domain reload)
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            s_PersistDir  = Path.Combine(projectRoot, "Library", "MosaicBridge");
            s_PersistPath = Path.Combine(s_PersistDir, "console.log");

            EnsureInitialized();
        }

        // ── Initialization ────────────────────────────────────────────────────

        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            ResolveReflection();
            OpenPersistentLog();

            // Seed ring buffer + file with any messages already in the Unity console
            // (e.g. compile errors, assembly-guard warnings logged before our subscription)
            var existing = ReadFromUnityConsole(true, true, true, MaxRingEntries);
            lock (s_Lock)
            {
                foreach (var e in existing)
                {
                    s_Ring.Add(e);
                    WriteToFile(e);
                }
            }

            Application.logMessageReceivedThreaded += OnLogMessage;
        }

        public static void Shutdown()
        {
            Application.logMessageReceivedThreaded -= OnLogMessage;
            _initialized = false;
            try { s_Writer?.Flush(); s_Writer?.Close(); s_Writer = null; } catch { }
        }

        // ── File management ───────────────────────────────────────────────────

        private static void OpenPersistentLog()
        {
            try
            {
                Directory.CreateDirectory(s_PersistDir);

                // Trim file if it has grown too large
                if (File.Exists(s_PersistPath) &&
                    new FileInfo(s_PersistPath).Length > MaxFileSizeBytes)
                {
                    TrimFile();
                }

                s_Writer = new StreamWriter(s_PersistPath, append: true, Encoding.UTF8)
                {
                    AutoFlush = true
                };
                // Session separator so AI can see where each domain reload begins
                s_Writer.WriteLine("=== Mosaic Bridge session " + DateTime.UtcNow.ToString("O") + " ===");
            }
            catch
            {
                s_Writer = null; // file I/O failed; degrade gracefully
            }
        }

        private static void TrimFile()
        {
            try
            {
                var lines = File.ReadAllLines(s_PersistPath);
                if (lines.Length > TrimToLines)
                {
                    var kept = new string[TrimToLines];
                    Array.Copy(lines, lines.Length - TrimToLines, kept, 0, TrimToLines);
                    File.WriteAllLines(s_PersistPath, kept, Encoding.UTF8);
                }
            }
            catch { }
        }

        private static void WriteToFile(ConsoleEntry e)
        {
            if (s_Writer == null) return;
            try
            {
                // Format: LEVEL|message|file:line
                string lvl = e.Level switch
                {
                    "Warning"   => "W",
                    "Error"     => "E",
                    "Exception" => "X",
                    "Assert"    => "A",
                    _           => "I"
                };
                string msg = (e.Message ?? "").Replace("\n", "\\n").Replace("\r", "");
                string loc = string.IsNullOrEmpty(e.File) ? "" : $"|{e.File}:{e.Line}";
                s_Writer.WriteLine($"{lvl}|{msg}{loc}");
            }
            catch { }
        }

        // ── Live ring buffer listener ─────────────────────────────────────────

        private static void OnLogMessage(string message, string stackTrace, LogType logType)
        {
            var entry = new ConsoleEntry
            {
                Message = message,
                Level   = LogTypeToLevel(logType),
                File    = ExtractFile(stackTrace),
                Line    = ExtractLine(stackTrace)
            };

            lock (s_Lock)
            {
                if (s_Ring.Count >= MaxRingEntries) s_Ring.RemoveAt(0);
                s_Ring.Add(entry);
                WriteToFile(entry);
            }
        }

        // ── Public read API ───────────────────────────────────────────────────

        /// <summary>
        /// Returns filtered entries. Priority:
        ///   1. Persistent file — survives Clear and domain reloads
        ///   2. Unity's LogEntries API — current console state
        ///   3. Live ring buffer — fallback
        /// </summary>
        public static List<ConsoleEntry> GetEntries(
            bool includeInfo, bool includeWarnings, bool includeErrors, int maxResults)
        {
            EnsureInitialized();

            // 1. Persistent file
            var fromFile = ReadFromFile(includeInfo, includeWarnings, includeErrors, maxResults);
            if (fromFile.Count > 0) return fromFile;

            // 2. Unity's internal LogEntries (current console window, cleared on Clear)
            var direct = ReadFromUnityConsole(includeInfo, includeWarnings, includeErrors, maxResults);
            if (direct.Count > 0) return direct;

            // 3. Ring buffer
            var live = new List<ConsoleEntry>();
            lock (s_Lock)
            {
                for (int i = s_Ring.Count - 1; i >= 0 && live.Count < maxResults; i--)
                {
                    var e = s_Ring[i];
                    if (Matches(e.Level, includeInfo, includeWarnings, includeErrors))
                        live.Add(e);
                }
                live.Reverse();
            }
            return live;
        }

        public static int TotalCount
        {
            get { lock (s_Lock) { return s_Ring.Count; } }
        }

        public static void Clear()
        {
            lock (s_Lock) { s_Ring.Clear(); }
            // Intentionally do NOT clear the persistent file — it's the history
        }

        // ── Persistent file reader ────────────────────────────────────────────

        private static List<ConsoleEntry> ReadFromFile(
            bool includeInfo, bool includeWarnings, bool includeErrors, int maxResults)
        {
            var result = new List<ConsoleEntry>();
            if (!File.Exists(s_PersistPath)) return result;

            try
            {
                string[] lines = File.ReadAllLines(s_PersistPath);

                // Scan from the end for maxResults matching lines
                int collected = 0;
                for (int i = lines.Length - 1; i >= 0 && collected < maxResults; i--)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line) || line.StartsWith("===")) continue;

                    // Parse: LEVEL|message|file:line
                    int sep1 = line.IndexOf('|');
                    if (sep1 < 0) continue;

                    string lvlCode = line.Substring(0, sep1);
                    string level   = lvlCode switch
                    {
                        "W" => "Warning",
                        "E" => "Error",
                        "X" => "Exception",
                        "A" => "Assert",
                        _   => "Info"
                    };

                    if (!Matches(level, includeInfo, includeWarnings, includeErrors)) continue;

                    int sep2    = line.LastIndexOf('|');
                    string msg  = sep2 > sep1
                        ? line.Substring(sep1 + 1, sep2 - sep1 - 1)
                        : line.Substring(sep1 + 1);
                    msg = msg.Replace("\\n", "\n");

                    string file = "";
                    int    lineNo = 0;
                    if (sep2 > sep1)
                    {
                        string loc = line.Substring(sep2 + 1);
                        int colon  = loc.LastIndexOf(':');
                        if (colon > 0)
                        {
                            file = loc.Substring(0, colon);
                            int.TryParse(loc.Substring(colon + 1), out lineNo);
                        }
                    }

                    result.Add(new ConsoleEntry
                    {
                        Message = msg,
                        Level   = level,
                        File    = file,
                        Line    = lineNo
                    });
                    collected++;
                }

                result.Reverse(); // chronological order
            }
            catch { }

            return result;
        }

        // ── Unity internal LogEntries ─────────────────────────────────────────

        private static void ResolveReflection()
        {
            if (_reflectionResolved) return;
            _reflectionResolved = true;
            try
            {
                _logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor")
                               ?? Type.GetType("UnityEditorInternal.LogEntries, UnityEditor");
                _logEntryType   = Type.GetType("UnityEditor.LogEntry, UnityEditor")
                               ?? Type.GetType("UnityEditorInternal.LogEntry, UnityEditor");
                if (_logEntriesType == null || _logEntryType == null) return;

                var sf = BindingFlags.Static | BindingFlags.Public;
                _startGetting     = _logEntriesType.GetMethod("StartGettingEntries", sf);
                _endGetting       = _logEntriesType.GetMethod("EndGettingEntries",   sf);
                _getEntryInternal = _logEntriesType.GetMethod("GetEntryInternal",    sf);
                _countProp        = _logEntriesType.GetProperty("count", sf)
                                 ?? _logEntriesType.GetProperty("Count", sf);

                var if_ = BindingFlags.Instance | BindingFlags.Public;
                _messageField = _logEntryType.GetField("message", if_)
                             ?? _logEntryType.GetField("Message", if_);
                _modeField    = _logEntryType.GetField("mode",    if_)
                             ?? _logEntryType.GetField("Mode",    if_);
            }
            catch { }
        }

        private static List<ConsoleEntry> ReadFromUnityConsole(
            bool includeInfo, bool includeWarnings, bool includeErrors, int maxResults)
        {
            var result = new List<ConsoleEntry>();
            if (!_reflectionResolved) ResolveReflection();
            if (_logEntriesType == null || _startGetting == null ||
                _endGetting == null || _getEntryInternal == null || _countProp == null)
                return result;

            try
            {
                int count = (int)_countProp.GetValue(null);
                if (count == 0) return result;

                _startGetting.Invoke(null, null);
                try
                {
                    int start = Math.Max(0, count - maxResults);
                    for (int i = start; i < count; i++)
                    {
                        var logEntry = Activator.CreateInstance(_logEntryType);
                        _getEntryInternal.Invoke(null, new object[] { i, logEntry });

                        string msg  = _messageField?.GetValue(logEntry) as string ?? "";
                        int    mode = _modeField != null ? (int)_modeField.GetValue(logEntry) : 0;

                        string level = ModeToLevel(mode);
                        if (!Matches(level, includeInfo, includeWarnings, includeErrors)) continue;

                        int nl = msg.IndexOf('\n');
                        string clean = nl > 0 ? msg.Substring(0, nl).Trim() : msg.Trim();
                        result.Add(new ConsoleEntry { Message = clean, Level = level });
                    }
                }
                finally { _endGetting.Invoke(null, null); }
            }
            catch { }

            return result;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool Matches(string level, bool info, bool warn, bool error)
        {
            return level switch
            {
                "Warning"   => warn,
                "Error"     => error,
                "Exception" => error,
                "Assert"    => error,
                _           => info
            };
        }

        // Unity LogEntry.mode bitmask:
        //   Error:   1(Error)|2(Assert)|8(Fatal)|32(AssetImportError)|128(ScriptingError)|1024(ScriptCompileError)
        //   Warning: 64(AssetImportWarning)|256(ScriptingWarning)|2048(ScriptCompileWarning)
        //   Info:    4(Log)|512(ScriptingLog)
        private static string ModeToLevel(int mode)
        {
            const int errorMask   = 1 | 2 | 8 | 32 | 128 | 1024;
            const int warningMask = 64 | 256 | 2048;
            if ((mode & errorMask)   != 0) return "Error";
            if ((mode & warningMask) != 0) return "Warning";
            return "Info";
        }

        private static string LogTypeToLevel(LogType t) => t switch
        {
            LogType.Error     => "Error",
            LogType.Exception => "Exception",
            LogType.Assert    => "Assert",
            LogType.Warning   => "Warning",
            _                 => "Info"
        };

        private static string ExtractFile(string st)
        {
            if (string.IsNullOrEmpty(st)) return "";
            int at = st.IndexOf("(at ", StringComparison.Ordinal);
            if (at < 0) return "";
            int colon = st.IndexOf(':', at + 4);
            if (colon < 0) return "";
            return st.Substring(at + 4, colon - at - 4);
        }

        private static int ExtractLine(string st)
        {
            if (string.IsNullOrEmpty(st)) return 0;
            int at = st.IndexOf("(at ", StringComparison.Ordinal);
            if (at < 0) return 0;
            int colon = st.IndexOf(':', at + 4);
            if (colon < 0) return 0;
            int end = st.IndexOf(')', colon);
            if (end < 0) return 0;
            return int.TryParse(st.Substring(colon + 1, end - colon - 1), out int n) ? n : 0;
        }
    }
}
