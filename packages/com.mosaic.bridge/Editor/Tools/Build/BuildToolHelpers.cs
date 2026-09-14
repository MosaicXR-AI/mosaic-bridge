using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Mosaic.Bridge.Tools.Build
{
    /// <summary>Shared between BuildTool (synchronous, CI-style) and BuildStartTool/BuildJobKind
    /// (O4 §3.4 — returns immediately, the actual BuildPipeline.BuildPlayer call runs later via
    /// delayCall so the HTTP response for build/start isn't the thing blocked for the build's
    /// full duration).</summary>
    internal static class BuildToolHelpers
    {
        internal static bool TryResolveTarget(string target, out BuildTarget buildTarget, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(target) || target == "current")
            {
                buildTarget = EditorUserBuildSettings.activeBuildTarget;
                return true;
            }
            try
            {
                buildTarget = (BuildTarget)Enum.Parse(typeof(BuildTarget), target, ignoreCase: true);
                return true;
            }
            catch
            {
                buildTarget = default;
                error = $"Unknown build target '{target}'. Valid values: StandaloneWindows64, StandaloneOSX, " +
                        "StandaloneLinux64, Android, iOS, WebGL";
                return false;
            }
        }

        internal static bool TryBuildOptions(BuildParams p, BuildTarget buildTarget,
                                              out BuildPlayerOptions options, out string error)
        {
            error = null;
            options = default;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var outputPath = string.IsNullOrEmpty(p.OutputPath)
                ? Path.Combine("Builds", Application.productName)
                : p.OutputPath;

            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                error = $"Failed to create output directory: {ex.Message}";
                return false;
            }

            options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = buildTarget,
                options = BuildOptions.None
            };
            if (p.Development) options.options |= BuildOptions.Development;
            if (p.AutoRunPlayer) options.options |= BuildOptions.AutoRunPlayer;
            if (p.ShowBuiltPlayer) options.options |= BuildOptions.ShowBuiltPlayer;
            return true;
        }

        internal static BuildPlayerResult ToResult(BuildReport report, BuildTarget target, double durationSeconds)
        {
            var allMessages = report.steps.SelectMany(s => s.messages).ToArray();
            return new BuildPlayerResult
            {
                BuildSucceeded = report.summary.result == BuildResult.Succeeded,
                OutputPath = report.summary.outputPath,
                TargetPlatform = target.ToString(),
                DurationSeconds = durationSeconds,
                Errors = allMessages
                    .Where(m => m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Assert)
                    .Select(m => m.content).ToArray(),
                Warnings = allMessages
                    .Where(m => m.type == LogType.Warning)
                    .Select(m => m.content).ToArray(),
            };
        }
    }
}
