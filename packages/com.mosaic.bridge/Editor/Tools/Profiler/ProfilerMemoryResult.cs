using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Profiling
{
    public sealed class ProfilerMemoryResult
    {
        public long TotalAllocatedMemory { get; set; }
        public long TotalReservedMemory { get; set; }
        public List<MemoryAreaInfo> Areas { get; set; }
    }

    public sealed class MemoryAreaInfo
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public long TotalSizeBytes { get; set; }
    }
}
