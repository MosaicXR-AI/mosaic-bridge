using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Build
{
    public static class BuildTool
    {
        [MosaicTool("build/build",
                    "Builds the Unity player for the specified target platform",
                    isReadOnly: false)]
        public static ToolResult<BuildPlayerResult> Build(BuildParams p)
        {
            // Resolve build target
            BuildTarget buildTarget;
            if (string.IsNullOrEmpty(p.Target) || p.Target == "current")
            {
                buildTarget = EditorUserBuildSettings.activeBuildTarget;
            }
            else
            {
                try
                {
                    buildTarget = (BuildTarget)Enum.Parse(typeof(BuildTarget), p.Target, ignoreCase: true);
                }
                catch
                {
                    return ToolResult<BuildPlayerResult>.Fail(
                        $"Unknown build target '{p.Target}'. Valid values: StandaloneWindows64, StandaloneOSX, StandaloneLinux64, Android, iOS, WebGL",
                        ErrorCodes.INVALID_PARAM);
                }
            }

            // Collect enabled scenes
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            // Resolve output path
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
                return ToolResult<BuildPlayerResult>.Fail(
                    $"Failed to create output directory: {ex.Message}",
                    ErrorCodes.INVALID_PARAM);
            }

            // Build options
            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = buildTarget,
                options = BuildOptions.None
            };

            if (p.Development)    buildOptions.options |= BuildOptions.Development;
            if (p.AutoRunPlayer)  buildOptions.options |= BuildOptions.AutoRunPlayer;
            if (p.ShowBuiltPlayer) buildOptions.options |= BuildOptions.ShowBuiltPlayer;

            // Time and run the build
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var report = BuildPipeline.BuildPlayer(buildOptions);
            sw.Stop();

            // Collect messages
            var allMessages = report.steps.SelectMany(s => s.messages).ToArray();
            var errors   = allMessages
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Assert)
                .Select(m => m.content)
                .ToArray();
            var warnings = allMessages
                .Where(m => m.type == LogType.Warning)
                .Select(m => m.content)
                .ToArray();

            var result = new BuildPlayerResult
            {
                BuildSucceeded  = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded,
                OutputPath      = report.summary.outputPath,
                TargetPlatform  = buildTarget.ToString(),
                DurationSeconds = sw.Elapsed.TotalSeconds,
                Errors          = errors,
                Warnings        = warnings
            };

            return ToolResult<BuildPlayerResult>.Ok(result);
        }
    }
}
