namespace Mosaic.Bridge.Tools.Navigation
{
    public sealed class NavigationBakeResult
    {
        public bool Baked { get; set; }
        public float AgentRadius { get; set; }
        public float AgentHeight { get; set; }
        public float StepHeight { get; set; }
        public float SlopeAngle { get; set; }
    }
}
