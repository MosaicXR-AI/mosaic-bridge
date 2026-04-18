namespace Mosaic.Bridge.Tools.Graphics
{
    public sealed class GraphicsSetPostProcessingResult
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public bool IsGlobal { get; set; }
        public float Weight { get; set; }
        public float Priority { get; set; }
        public string ProfilePath { get; set; }
    }
}
