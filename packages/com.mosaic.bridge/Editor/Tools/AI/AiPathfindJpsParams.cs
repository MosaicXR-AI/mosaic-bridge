using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiPathfindJpsParams
    {
        [Required] public int    GridWidth         { get; set; }
        [Required] public int    GridHeight        { get; set; }
        [Required] public int[]  Start             { get; set; }
        [Required] public int[]  End               { get; set; }
        public int[][]           Obstacles         { get; set; }
        public bool              DiagonalMovement  { get; set; } = true;
        public string            Heuristic         { get; set; } = "octile";
    }
}
