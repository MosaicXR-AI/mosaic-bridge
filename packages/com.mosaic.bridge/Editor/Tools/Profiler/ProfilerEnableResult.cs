namespace Mosaic.Bridge.Tools.Profiling
{
    public sealed class ProfilerEnableResult
    {
        public string Action { get; set; }
        public bool Enabled { get; set; }
        public bool DeepProfiling { get; set; }
        public string LogFilePath { get; set; }
    }
}
