using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Profiling
{
    public sealed class ProfilerEnableParams
    {
        [Required] public string Action { get; set; } // start, stop, deep-profile
        public string LogFilePath { get; set; }
    }
}
