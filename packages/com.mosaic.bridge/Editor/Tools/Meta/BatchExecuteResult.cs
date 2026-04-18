using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Meta
{
    public sealed class BatchExecuteResult
    {
        public int TotalCalls { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public int SkippedCount { get; set; }
        public long TotalDurationMs { get; set; }
        public List<BatchCallResult> Results { get; set; }
    }

    public sealed class BatchCallResult
    {
        public string ToolName { get; set; }
        public bool Success { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }
        public long DurationMs { get; set; }
        public bool Skipped { get; set; }
    }
}
