using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Graphics
{
    public sealed class GraphicsSetPostProcessingParams
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string ProfilePath { get; set; }
        public bool IsGlobal { get; set; } = true;
        public float Weight { get; set; } = 1.0f;
        public float Priority { get; set; }
    }
}
