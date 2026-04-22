namespace Mosaic.Bridge.Tools.ConsoleTools
{
    public sealed class ConsoleGetErrorsParams
    {
        /// <summary>Include warning-level entries. Default: true.</summary>
        public bool IncludeWarnings { get; set; } = true;

        /// <summary>Include info/log-level entries. Default: false.</summary>
        public bool IncludeInfo { get; set; } = false;

        /// <summary>Maximum number of entries to return. Default: 50.</summary>
        public int MaxResults { get; set; } = 50;
    }
}
