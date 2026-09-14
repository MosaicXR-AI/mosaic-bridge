using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Build
{
    public static class BuildTool
    {
        [MosaicTool("build/build",
                    "Builds the Unity player for the specified target platform. SYNCHRONOUS — blocks until the " +
                    "build finishes, which can be tens of minutes for a large project and risks an HTTP timeout " +
                    "on the caller's side. Prefer build/start, which returns a JobId immediately and reports " +
                    "completion via job/status.",
                    isReadOnly: false)]
        public static ToolResult<BuildPlayerResult> Build(BuildParams p)
        {
            if (!BuildToolHelpers.TryResolveTarget(p.Target, out var buildTarget, out var targetError))
                return ToolResult<BuildPlayerResult>.Fail(targetError, ErrorCodes.INVALID_PARAM);

            if (!BuildToolHelpers.TryBuildOptions(p, buildTarget, out var buildOptions, out var optionsError))
                return ToolResult<BuildPlayerResult>.Fail(optionsError, ErrorCodes.INVALID_PARAM);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var report = BuildPipeline.BuildPlayer(buildOptions);
            sw.Stop();

            return ToolResult<BuildPlayerResult>.Ok(
                BuildToolHelpers.ToResult(report, buildTarget, sw.Elapsed.TotalSeconds));
        }
    }
}
