using UnityEngine.Profiling;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Profiling
{
    public static class ProfilerStatsTool
    {
        [MosaicTool("profiler/stats",
                    "Returns current memory statistics from the Unity Profiler",
                    isReadOnly: true)]
        public static ToolResult<ProfilerStatsResult> Stats(ProfilerStatsParams p)
        {
            return ToolResult<ProfilerStatsResult>.Ok(new ProfilerStatsResult
            {
                TotalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong(),
                TotalReservedMemory = Profiler.GetTotalReservedMemoryLong(),
                TotalUnusedReservedMemory = Profiler.GetTotalUnusedReservedMemoryLong(),
                MonoUsedSize = Profiler.GetMonoUsedSizeLong(),
                MonoHeapSize = Profiler.GetMonoHeapSizeLong(),
                ProfilerEnabled = Profiler.enabled
            });
        }
    }
}
