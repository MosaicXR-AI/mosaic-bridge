using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public sealed class NavPathfindAStarParams
    {
        [Required] public int  GridWidth   { get; set; }
        [Required] public int  GridHeight  { get; set; }
        [Required] public int  StartX     { get; set; }
        [Required] public int  StartY     { get; set; }
        [Required] public int  EndX       { get; set; }
        [Required] public int  EndY       { get; set; }
        public int[]           Obstacles  { get; set; }
        public bool            AllowDiagonal       { get; set; } = true;
        public bool            CreateVisualization  { get; set; }
    }
}
