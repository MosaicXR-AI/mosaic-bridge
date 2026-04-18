namespace Mosaic.Bridge.Tools.Profiling
{
    public sealed class ProfilerStatsResult
    {
        public long TotalAllocatedMemory { get; set; }
        public long TotalReservedMemory { get; set; }
        public long TotalUnusedReservedMemory { get; set; }
        public long MonoUsedSize { get; set; }
        public long MonoHeapSize { get; set; }
        public bool ProfilerEnabled { get; set; }
    }
}
