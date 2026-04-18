using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public sealed class NavIKFabrikParams
    {
        public int?    RootInstanceId  { get; set; }
        public string  RootName        { get; set; }
        [Required] public float[] TargetPosition { get; set; }
        public int?    ChainLength     { get; set; }
        public int?    Iterations      { get; set; }
        public float?  Tolerance       { get; set; }
        public string  OutputDirectory { get; set; }
    }
}
