using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiFlowFieldParams
    {
        [Required] public int      GridWidth                { get; set; }
        [Required] public int      GridHeight               { get; set; }
        [Required] public int[][]  Targets                  { get; set; }
        public int[][]             Obstacles                { get; set; }
        public float[]             CostField                { get; set; }
        public bool                Smoothing                { get; set; }
        public bool                CreateDebugVisualization  { get; set; }
        public string              VisualizationParent      { get; set; }
        public float               CellSize                 { get; set; } = 1.0f;
    }
}
