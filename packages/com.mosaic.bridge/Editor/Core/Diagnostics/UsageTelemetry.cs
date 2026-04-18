using System;
using System.Collections.Generic;
using Mosaic.Bridge.Core.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Mosaic.Bridge.Core.Diagnostics
{
    /// <summary>
    /// Story 10.2 — Local-only pilot telemetry. Tracks tool call counts,
    /// KB query counts, and execution mode distribution per day.
    /// All data stored in EditorPrefs as JSON blobs keyed by date.
    /// Opt-in via FeatureFlags.TelemetryEnabled.
    /// </summary>
    public static class UsageTelemetry
    {
        private const string KeyPrefix = "MosaicBridge.Telemetry.";

        /// <summary>
        /// Records a tool call. Called from ExecutionPipeline after execution completes.
        /// No-op if telemetry is disabled.
        /// </summary>
        public static void RecordToolCall(string toolName, string executionMode, double durationMs)
        {
            if (!FeatureFlags.IsEnabled(FeatureFlags.TelemetryEnabled))
                return;

            var today = GetTodayBlob();

            // Tool call counts per category
            var category = ExtractCategory(toolName);
            IncrementDict(today, "toolCalls", category);

            // Execution mode distribution
            IncrementDict(today, "executionModes", executionMode ?? "direct");

            // Total duration accumulator for average calculation
            var stats = GetOrCreateObject(today, "stats");
            stats["totalCalls"] = (stats.Value<int>("totalCalls")) + 1;
            stats["totalDurationMs"] = (stats.Value<double>("totalDurationMs")) + durationMs;

            SaveTodayBlob(today);
        }

        /// <summary>
        /// Records a knowledge base query. Called from KnowledgeAdvisorStage.
        /// No-op if telemetry is disabled.
        /// </summary>
        public static void RecordKbQuery(string category)
        {
            if (!FeatureFlags.IsEnabled(FeatureFlags.TelemetryEnabled))
                return;

            var today = GetTodayBlob();
            IncrementDict(today, "kbQueries", category ?? "unknown");
            SaveTodayBlob(today);
        }

        /// <summary>Returns today's telemetry summary.</summary>
        public static DailySummary GetDailySummary()
        {
            return BuildSummary(GetTodayBlob());
        }

        /// <summary>Returns an aggregated summary of the last 7 days.</summary>
        public static DailySummary GetWeeklySummary()
        {
            var aggregate = new JObject();
            var now = DateTime.UtcNow.Date;

            for (int i = 0; i < 7; i++)
            {
                var date = now.AddDays(-i).ToString("yyyy-MM-dd");
                var key = KeyPrefix + date;
                var raw = EditorPrefs.GetString(key, "");
                if (string.IsNullOrEmpty(raw))
                    continue;

                try
                {
                    var dayBlob = JObject.Parse(raw);
                    MergeInto(aggregate, dayBlob);
                }
                catch
                {
                    // Skip corrupt day data
                }
            }

            return BuildSummary(aggregate);
        }

        // ---------------------------------------------------------------
        //  Internal helpers
        // ---------------------------------------------------------------

        private static string TodayKey => KeyPrefix + DateTime.UtcNow.ToString("yyyy-MM-dd");

        private static JObject GetTodayBlob()
        {
            var raw = EditorPrefs.GetString(TodayKey, "");
            if (!string.IsNullOrEmpty(raw))
            {
                try { return JObject.Parse(raw); }
                catch { /* corrupt — start fresh */ }
            }
            return new JObject();
        }

        private static void SaveTodayBlob(JObject blob)
        {
            EditorPrefs.SetString(TodayKey, blob.ToString(Formatting.None));
        }

        private static string ExtractCategory(string toolName)
        {
            if (string.IsNullOrEmpty(toolName))
                return "unknown";

            // Format: "mosaic_category_action"
            var parts = toolName.Split('_');
            return parts.Length >= 2 ? parts[1] : toolName;
        }

        private static void IncrementDict(JObject blob, string section, string key)
        {
            var dict = GetOrCreateObject(blob, section);
            dict[key] = (dict.Value<int?>(key) ?? 0) + 1;
        }

        private static JObject GetOrCreateObject(JObject parent, string key)
        {
            if (parent[key] is JObject obj)
                return obj;

            var newObj = new JObject();
            parent[key] = newObj;
            return newObj;
        }

        private static void MergeInto(JObject aggregate, JObject dayBlob)
        {
            foreach (var prop in dayBlob.Properties())
            {
                if (prop.Value is JObject dayDict)
                {
                    var aggDict = GetOrCreateObject(aggregate, prop.Name);
                    foreach (var inner in dayDict.Properties())
                    {
                        if (inner.Value.Type == JTokenType.Integer || inner.Value.Type == JTokenType.Float)
                        {
                            aggDict[inner.Name] = (aggDict.Value<double?>(inner.Name) ?? 0) + inner.Value.Value<double>();
                        }
                    }
                }
            }
        }

        private static DailySummary BuildSummary(JObject blob)
        {
            var summary = new DailySummary();

            if (blob["toolCalls"] is JObject toolCalls)
            {
                foreach (var prop in toolCalls.Properties())
                    summary.ToolCallsByCategory[prop.Name] = prop.Value.Value<int>();
            }

            if (blob["kbQueries"] is JObject kbQueries)
            {
                foreach (var prop in kbQueries.Properties())
                    summary.KbQueriesByCategory[prop.Name] = prop.Value.Value<int>();
            }

            if (blob["executionModes"] is JObject modes)
            {
                foreach (var prop in modes.Properties())
                    summary.ExecutionModeDistribution[prop.Name] = prop.Value.Value<int>();
            }

            if (blob["stats"] is JObject stats)
            {
                summary.TotalCalls = stats.Value<int>("totalCalls");
                summary.TotalDurationMs = stats.Value<double>("totalDurationMs");
                summary.AverageDurationMs = summary.TotalCalls > 0
                    ? summary.TotalDurationMs / summary.TotalCalls
                    : 0;
            }

            return summary;
        }
    }

    /// <summary>Telemetry summary for a day or aggregated period.</summary>
    public sealed class DailySummary
    {
        public Dictionary<string, int> ToolCallsByCategory { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> KbQueriesByCategory { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> ExecutionModeDistribution { get; set; } = new Dictionary<string, int>();
        public int TotalCalls { get; set; }
        public double TotalDurationMs { get; set; }
        public double AverageDurationMs { get; set; }
    }
}
