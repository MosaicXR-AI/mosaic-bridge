using System;
using System.Collections.Generic;
using Mosaic.Bridge.Core.Security;

namespace Mosaic.Bridge.Core.Diagnostics
{
    /// <summary>
    /// Records tool call metrics: name, duration, success/failure, error codes.
    /// Static ring buffer of last 200 calls. Thread-safe.
    /// </summary>
    public static class ToolCallLogger
    {
        private static readonly object _lock = new object();
        private static readonly List<ToolCallRecord> _records = new List<ToolCallRecord>();
        private const int MaxRecords = 200;

        public static void Record(string toolName, int statusCode, double durationMs, string errorCode = null)
        {
            var record = new ToolCallRecord
            {
                ToolName = toolName,
                StatusCode = statusCode,
                DurationMs = durationMs,
                ErrorCode = LogRedactor.Redact(errorCode),
                Timestamp = DateTime.UtcNow,
                IsSuccess = statusCode >= 200 && statusCode < 300
            };

            lock (_lock)
            {
                if (_records.Count >= MaxRecords)
                    _records.RemoveAt(0);
                _records.Add(record);
            }
        }

        public static List<ToolCallRecord> GetRecords(int max = 50)
        {
            lock (_lock)
            {
                var count = Math.Min(max, _records.Count);
                var result = new List<ToolCallRecord>(count);
                for (int i = _records.Count - count; i < _records.Count; i++)
                    result.Add(_records[i]);
                return result;
            }
        }

        public static DiagnosticsSummary GetSummary()
        {
            lock (_lock)
            {
                var summary = new DiagnosticsSummary();
                summary.TotalCalls = _records.Count;

                double totalMs = 0;
                foreach (var r in _records)
                {
                    if (r.IsSuccess) summary.SuccessCount++;
                    else summary.FailureCount++;
                    totalMs += r.DurationMs;
                }

                summary.AverageDurationMs = _records.Count > 0 ? totalMs / _records.Count : 0;
                summary.ErrorRate = _records.Count > 0 ? (double)summary.FailureCount / _records.Count * 100 : 0;

                return summary;
            }
        }

        public static void Clear()
        {
            lock (_lock) { _records.Clear(); }
        }
    }

    public sealed class ToolCallRecord
    {
        public string ToolName { get; set; }
        public int StatusCode { get; set; }
        public double DurationMs { get; set; }
        public string ErrorCode { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsSuccess { get; set; }
    }

    public sealed class DiagnosticsSummary
    {
        public int TotalCalls { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double AverageDurationMs { get; set; }
        public double ErrorRate { get; set; }
    }
}
