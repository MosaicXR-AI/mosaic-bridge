using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public sealed class NavSpherePathfindParams
    {
        public float?  SphereRadius         { get; set; }
        public int?    Resolution           { get; set; }
        [Required] public float  StartLat   { get; set; }
        [Required] public float  StartLon   { get; set; }
        [Required] public float  EndLat     { get; set; }
        [Required] public float  EndLon     { get; set; }
        public float[] Obstacles            { get; set; }
        public bool    CreateVisualization  { get; set; }
    }
}
