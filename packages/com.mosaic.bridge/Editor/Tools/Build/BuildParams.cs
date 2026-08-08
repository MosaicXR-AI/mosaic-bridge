namespace Mosaic.Bridge.Tools.Build
{
    public sealed class BuildParams
    {
        public string Target { get; set; } = "current";
        public string OutputPath { get; set; }
        public bool Development { get; set; } = false;
        public bool AutoRunPlayer { get; set; } = false;
        public bool ShowBuiltPlayer { get; set; } = false;
    }
}
