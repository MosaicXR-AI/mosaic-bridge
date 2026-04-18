namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiPathfindJpsResult
    {
        public int[][] Path          { get; set; }
        public float   PathLength    { get; set; }
        public int     NodesExplored { get; set; }
        public bool    Success       { get; set; }
        public int     GridWidth     { get; set; }
        public int     GridHeight    { get; set; }
    }
}
