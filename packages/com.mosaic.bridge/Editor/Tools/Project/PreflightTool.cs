using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Tools.Materials;

namespace Mosaic.Bridge.Tools.Project
{
    public static class PreflightTool
    {
        [MosaicTool("project/preflight",
                    "Returns a snapshot of the current Unity project environment: Unity version, " +
                    "active render pipeline (BuiltIn/URP/HDRP), suggested color property name (_Color vs _BaseColor), " +
                    "active scene info, installed UPM packages, and recent console errors. " +
                    "Call this at the START of any session to avoid render-pipeline mismatches and missing-package failures.",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<PreflightResult> Execute(PreflightParams p)
        {
            // ── Render pipeline ─────────────────────────────────────────────
            string pipeline  = MaterialCreateTool.DetectRenderPipeline();
            string colorProp = (pipeline == "URP" || pipeline == "HDRP") ? "_BaseColor" : "_Color";

            // ── Active scene ─────────────────────────────────────────────────
            var scene     = EditorSceneManager.GetActiveScene();
            string scenePath = scene.path;
            string sceneName = scene.name;
            bool isDirty     = scene.isDirty;

            // ── Installed packages ───────────────────────────────────────────
            string[] packages = GetInstalledPackages();

            // ── Console errors / warnings ────────────────────────────────────
            GetConsoleCounts(out int errorCount, out int warnCount, out string[] recentErrors);

            return ToolResult<PreflightResult>.Ok(new PreflightResult
            {
                UnityVersion      = Application.unityVersion,
                RenderPipeline    = pipeline,
                ColorProperty     = colorProp,
                ActiveScenePath   = scenePath,
                ActiveSceneName   = sceneName,
                SceneIsDirty      = isDirty,
                InstalledPackages = packages,
                ConsoleErrorCount = errorCount,
                ConsoleWarnCount  = warnCount,
                RecentErrors      = recentErrors
            });
        }

        private static string[] GetInstalledPackages()
        {
            try
            {
                string manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
                if (!File.Exists(manifestPath)) return Array.Empty<string>();
                string manifest = File.ReadAllText(manifestPath);
                // Extract "com.xxx..." keys — simple JSON key scan
                var packages = new List<string>();
                int idx = 0;
                while ((idx = manifest.IndexOf("\"com.", idx, StringComparison.Ordinal)) >= 0)
                {
                    int end = manifest.IndexOf("\"", idx + 1);
                    if (end > idx) packages.Add(manifest.Substring(idx + 1, end - idx - 1));
                    idx = end + 1;
                }
                return packages.Distinct().OrderBy(x => x).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static void GetConsoleCounts(out int errors, out int warnings, out string[] recent)
        {
            errors   = 0;
            warnings = 0;
            recent   = Array.Empty<string>();

            try
            {
                // LogEntries is internal Unity API — access via reflection
                var logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor");
                if (logEntriesType == null) return;

                var getCountsByType = logEntriesType.GetMethod("GetCountsByType",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (getCountsByType != null)
                {
                    var args = new object[] { 0, 0, 0 };
                    getCountsByType.Invoke(null, args);
                    errors   = (int)args[0];
                    warnings = (int)args[1];
                }

                // Grab recent error messages
                var startGetting = logEntriesType.GetMethod("StartGettingEntries",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var endGetting = logEntriesType.GetMethod("EndGettingEntries",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var getEntry  = logEntriesType.GetMethod("GetEntryInternal",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                var logEntryType = Type.GetType("UnityEditor.LogEntry,UnityEditor");

                if (startGetting != null && endGetting != null && getEntry != null && logEntryType != null)
                {
                    int total = (int)startGetting.Invoke(null, null);
                    var recentList = new List<string>();
                    int start = Math.Max(0, total - 5);
                    for (int i = start; i < total && recentList.Count < 5; i++)
                    {
                        var entry = Activator.CreateInstance(logEntryType);
                        getEntry.Invoke(null, new object[] { i, entry });
                        var msgProp = logEntryType.GetField("message",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var modeProp = logEntryType.GetField("mode",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (msgProp != null && modeProp != null)
                        {
                            int mode = (int)modeProp.GetValue(entry);
                            if ((mode & 1) != 0) // Error bit
                                recentList.Add(msgProp.GetValue(entry)?.ToString() ?? "");
                        }
                    }
                    endGetting.Invoke(null, null);
                    recent = recentList.ToArray();
                }
            }
            catch { }
        }
    }
}
