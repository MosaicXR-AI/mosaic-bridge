using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mosaic.Bridge.Contracts.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Core.Diagnostics
{
    /// <summary>
    /// Writes structured JSON log entries to the runtime directory.
    /// Each entry is a single JSON line (JSONL format) for easy parsing.
    /// Rotates files daily, keeps last 14 days.
    /// Writes are fire-and-forget via ConcurrentQueue drained by a background Task.
    /// Thread-safe: callers never block on disk I/O.
    /// </summary>
    public sealed class FileLogger : IMosaicLogger, IDisposable
    {
        private readonly string _logDirectory;
        private readonly int _retentionDays;

        // Background drain infrastructure
        private readonly ConcurrentQueue<string> _writeQueue = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _drainSignal = new SemaphoreSlim(0, 1);
        private readonly object _writerLock = new object();  // guards StreamWriter only
        private Task _drainTask;
        private volatile int _queueDropWarningCounter;
        private const int QueueCapacity = 1000;

        private StreamWriter _writer;
        private string _currentDate;
        private volatile bool _disposed;

        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        public FileLogger(string runtimeDirectory, int retentionDays = 14)
        {
            _retentionDays = retentionDays;
            _logDirectory = Path.Combine(runtimeDirectory, "logs");
            Directory.CreateDirectory(_logDirectory);
            OpenLogFile();
            CleanupOldLogs();
            _drainTask = Task.Run(DrainLoopAsync);
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
        /// Signals the drain task to flush pending entries first, then reads the file
        /// outside the write lock so concurrent writes are not blocked.
        /// </summary>
        public List<string> ReadLastEntries(int count = 100)
        {
            // Signal drain and wait briefly (up to 200ms) for pending queue entries to land on disk
            SignalDrain();
            var deadline = DateTime.UtcNow.AddMilliseconds(200);
            while (_writeQueue.Count > 0 && DateTime.UtcNow < deadline)
                Thread.Sleep(5);

            // Short critical section: flush the StreamWriter only
            lock (_writerLock)
            {
                try { _writer?.Flush(); } catch { }
            }

            // Read file entirely outside the lock — writes are not blocked during this read
            var result = new List<string>();
            try
            {
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var filePath = GetLogFilePath(today);
                if (!File.Exists(filePath)) return result;

                var allLines = File.ReadAllLines(filePath);
                var start = Math.Max(0, allLines.Length - count);
                for (int i = start; i < allLines.Length; i++)
                    if (!string.IsNullOrWhiteSpace(allLines[i]))
                        result.Add(allLines[i]);
            }
            catch { /* best-effort */ }

            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Wake the background task so it exits its WaitAsync promptly
            SignalDrain();
            try { _drainTask?.Wait(TimeSpan.FromMilliseconds(200)); } catch { }

            // Drain any entries the background task didn't reach before exiting
            lock (_writerLock)
            {
                while (_writeQueue.TryDequeue(out var line))
                    try { _writer?.WriteLine(line); } catch { }

                try { _writer?.Flush(); } catch { }
                _writer?.Dispose();
                _writer = null;
            }
        }

        /// <summary>
        /// Writes a structured tool-call trace entry. Only safe fields are recorded —
        /// no parameter values, asset names, prompt text, or other sensitive data.
        /// </summary>
        public void WriteToolCall(string toolName, int statusCode, double durationMs, string errorCode = null)
        {
            if (_disposed) return;

            string line;
            try
            {
                var obj = new JObject
                {
                    ["ts"]           = DateTime.UtcNow.ToString("o"),
                    ["level"]        = "TOOL_CALL",
                    ["tool"]         = toolName,
                    ["status"]       = statusCode,
                    ["durationMs"]   = Math.Round(durationMs, 2),
                    ["success"]      = statusCode >= 200 && statusCode < 300,
                    ["errorCode"]    = errorCode != null ? (JToken)errorCode : JValue.CreateNull(),
                    ["schemaVersion"] = 1
                };
                line = obj.ToString(Formatting.None);
            }
            catch { return; }

            Enqueue(line);
        }

        private void WriteEntry(string level, string message, (string Key, object Value)[] context, Exception exception = null)
        {
            if (_disposed) return;

            // Serialize on calling thread (CPU only — no I/O), then enqueue
            string line;
            try
            {
                var obj = new JObject
                {
                    ["ts"] = DateTime.UtcNow.ToString("o"),
                    ["level"] = level,
                    ["msg"] = message
                };

                if (context != null)
                    foreach (var (key, value) in context)
                        obj[key] = value != null ? JToken.FromObject(value) : JValue.CreateNull();

                if (exception != null)
                    obj["exception"] = exception.ToString();

                line = obj.ToString(Formatting.None);
            }
            catch { return; }  // serialization failure: drop silently

            Enqueue(line);
        }

        private void Enqueue(string line)
        {
            // Queue cap: discard oldest entry if at capacity
            if (_writeQueue.Count >= QueueCapacity)
            {
                _writeQueue.TryDequeue(out _);
                // Mask to non-negative before modulo: int.MaxValue wrap produces int.MinValue,
                // and negative % QueueCapacity is negative (never == 1) in C#.
                if ((Interlocked.Increment(ref _queueDropWarningCounter) & int.MaxValue) % QueueCapacity == 1)
                    UnityEngine.Debug.LogWarning("[Mosaic.Bridge] FileLogger queue cap reached; oldest entry dropped");
            }

            _writeQueue.Enqueue(line);
            SignalDrain();
        }

        private async Task DrainLoopAsync()
        {
            while (!_disposed)
            {
                try
                {
                    // Wait for signal or timeout (timeout handles race where signal arrives before WaitAsync)
                    await _drainSignal.WaitAsync(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

                    bool wroteAny = false;
                    while (_writeQueue.TryDequeue(out var line))
                    {
                        lock (_writerLock)
                        {
                            // Do not return early here — the item is already dequeued.
                            // Write it even when _disposed; Dispose waits for this task
                            // before closing the writer, so _writer is still valid.
                            RotateIfNeeded();
                            try { _writer.WriteLine(line); }
                            catch { /* best-effort */ }
                        }
                        wroteAny = true;
                    }

                    // Explicit flush after emptying the batch
                    if (wroteAny)
                    {
                        lock (_writerLock)
                        {
                            if (!_disposed) try { _writer.Flush(); } catch { }
                        }
                    }
                }
                catch (Exception ex) when (!_disposed)
                {
                    UnityEngine.Debug.LogWarning($"[Mosaic.Bridge] FileLogger background drain restarted: {ex.Message}");
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
        }

        private void SignalDrain()
        {
            // Non-blocking: only release if the semaphore is not already signalled
            if (_drainSignal.CurrentCount == 0)
            {
                try { _drainSignal.Release(); }
                catch (SemaphoreFullException) { /* already signalled — fine */ }
            }
        }

        private void OpenLogFile()
        {
            _currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var filePath = GetLogFilePath(_currentDate);

            _writer = new StreamWriter(filePath, append: true, encoding: System.Text.Encoding.UTF8)
            {
                AutoFlush = false  // drain task flushes explicitly after each batch
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
                            File.Delete(file);
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
