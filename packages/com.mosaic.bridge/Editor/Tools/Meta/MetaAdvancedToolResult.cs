namespace Mosaic.Bridge.Tools.Meta
{
    public sealed class MetaAdvancedToolResult
    {
        public string ToolName { get; set; }
        public bool Success { get; set; }
        public object Data { get; set; }
        public string Error { get; set; }
        public long DurationMs { get; set; }
    }
}
