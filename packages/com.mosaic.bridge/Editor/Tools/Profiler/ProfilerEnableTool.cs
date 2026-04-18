using UnityEngine.Profiling;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Profiling
{
    public static class ProfilerEnableTool
    {
        [MosaicTool("profiler/enable",
                    "Starts or stops the Unity Profiler, with optional deep profiling",
                    isReadOnly: false)]
        public static ToolResult<ProfilerEnableResult> Enable(ProfilerEnableParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "start":
                    Profiler.enabled = true;
                    if (!string.IsNullOrEmpty(p.LogFilePath))
                    {
                        Profiler.logFile = p.LogFilePath;
                        Profiler.enableBinaryLog = true;
                    }
                    return ToolResult<ProfilerEnableResult>.Ok(new ProfilerEnableResult
                    {
                        Action = "start",
                        Enabled = true,
                        DeepProfiling = false,
                        LogFilePath = Profiler.logFile
                    });

                case "stop":
                    Profiler.enableBinaryLog = false;
                    Profiler.logFile = "";
                    Profiler.enabled = false;
                    return ToolResult<ProfilerEnableResult>.Ok(new ProfilerEnableResult
                    {
                        Action = "stop",
                        Enabled = false,
                        DeepProfiling = false,
                        LogFilePath = null
                    });

                case "deep-profile":
                    Profiler.enabled = true;
                    if (!string.IsNullOrEmpty(p.LogFilePath))
                    {
                        Profiler.logFile = p.LogFilePath;
                        Profiler.enableBinaryLog = true;
                    }
                    return ToolResult<ProfilerEnableResult>.Ok(new ProfilerEnableResult
                    {
                        Action = "deep-profile",
                        Enabled = true,
                        DeepProfiling = true,
                        LogFilePath = Profiler.logFile
                    });

                default:
                    return ToolResult<ProfilerEnableResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: start, stop, deep-profile",
                        ErrorCodes.INVALID_PARAM);
            }
        }
    }
}
