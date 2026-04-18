namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiFlowFieldResult
    {
        public int        GridWidth                { get; set; }
        public int        GridHeight               { get; set; }
        public float[][]  FlowField                { get; set; }
        public float[]    DistanceField            { get; set; }
        public int        ReachableCells           { get; set; }
        public int        UnreachableCells         { get; set; }
        public string     VisualizationObjectName  { get; set; }
    }
}
