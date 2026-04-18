using System;
using System.IO;
using Mosaic.Bridge.Core.Bootstrap;
using Mosaic.Bridge.Core.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Story 10.4 — Static utility that collects diagnostic data and
    /// saves a structured JSON issue report to {RuntimeDir}/reports/.
    /// </summary>
    public static class ReportIssueHelper
    {
        /// <summary>
        /// Collects all diagnostic data and saves an issue report.
        /// Returns the saved file path, or null on failure.
        /// </summary>
        public static string CreateReport()
        {
            var runtimeDir = BridgeBootstrap.RuntimeDirectory;
            if (string.IsNullOrEmpty(runtimeDir))
            {
                Debug.LogWarning("[Mosaic.Bridge] Cannot create issue report: runtime directory not available.");
                return null;
            }

            var reportsDir = Path.Combine(runtimeDir, "reports");
            Directory.CreateDirectory(reportsDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var filePath = Path.Combine(reportsDir, $"issue-{timestamp}.json");

            var report = new JObject
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["systemInfo"] = CollectSystemInfo(),
                ["recentLogEntries"] = CollectLogEntries(20),
                ["recentToolCalls"] = CollectToolCalls(10),
                ["sceneInfo"] = CollectSceneInfo()
            };

            File.WriteAllText(filePath, report.ToString(Formatting.Indented));

            BridgeBootstrap.Logger?.Info("Issue report saved", ("path", (object)filePath));

            return filePath;
        }

        /// <summary>Collects system and bridge state information.</summary>
        internal static JObject CollectSystemInfo()
        {
            return new JObject
            {
                ["unityVersion"] = Application.unityVersion,
                ["os"] = SystemInfo.operatingSystem,
                ["bridgeState"] = BridgeBootstrap.State.ToString(),
                ["bridgePort"] = BridgeBootstrap.Server?.Port ?? 0,
                ["toolCount"] = BridgeBootstrap.ToolRegistry?.Count ?? 0,
                ["mcpPid"] = BridgeBootstrap.McpProcess?.CurrentPid ?? 0,
                ["platform"] = Application.platform.ToString(),
                ["systemMemoryMb"] = SystemInfo.systemMemorySize,
                ["graphicsDevice"] = SystemInfo.graphicsDeviceName
            };
        }

        /// <summary>Collects the last N entries from the FileLogger.</summary>
        private static JArray CollectLogEntries(int count)
        {
            var arr = new JArray();

            var fileLog = BridgeBootstrap.FileLog;
            if (fileLog == null)
                return arr;

            var entries = fileLog.ReadLastEntries(count);
            foreach (var entry in entries)
            {
                try
                {
                    arr.Add(JToken.Parse(entry));
                }
                catch
                {
                    arr.Add(entry);
                }
            }

            return arr;
        }

        /// <summary>Collects the last N tool call records.</summary>
        private static JArray CollectToolCalls(int count)
        {
            var arr = new JArray();
            var records = ToolCallLogger.GetRecords(count);

            foreach (var r in records)
            {
                arr.Add(new JObject
                {
                    ["tool"] = r.ToolName,
                    ["status"] = r.StatusCode,
                    ["durationMs"] = r.DurationMs,
                    ["errorCode"] = r.ErrorCode,
                    ["timestamp"] = r.Timestamp.ToString("o"),
                    ["isSuccess"] = r.IsSuccess
                });
            }

            return arr;
        }

        /// <summary>Collects current scene information.</summary>
        private static JObject CollectSceneInfo()
        {
            var scene = SceneManager.GetActiveScene();
            return new JObject
            {
                ["name"] = scene.name,
                ["path"] = scene.path,
                ["isDirty"] = scene.isDirty,
                ["rootCount"] = scene.rootCount,
                ["isLoaded"] = scene.isLoaded
            };
        }
    }
}
