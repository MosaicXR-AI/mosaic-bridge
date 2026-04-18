using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Profiling
{
    public static class ProfilerFrameDataTool
    {
        [MosaicTool("profiler/frame-data",
                    "Returns current frame timing data including deltaTime, FPS, and frame count",
                    isReadOnly: true)]
        public static ToolResult<ProfilerFrameDataResult> FrameData(ProfilerFrameDataParams p)
        {
            float dt = Time.deltaTime;
            float fps = dt > 0f ? 1f / dt : 0f;

            return ToolResult<ProfilerFrameDataResult>.Ok(new ProfilerFrameDataResult
            {
                DeltaTime = dt,
                Fps = fps,
                RealtimeSinceStartup = Time.realtimeSinceStartup,
                FrameCount = Time.frameCount,
                IsPlaying = Application.isPlaying
            });
        }
    }
}
