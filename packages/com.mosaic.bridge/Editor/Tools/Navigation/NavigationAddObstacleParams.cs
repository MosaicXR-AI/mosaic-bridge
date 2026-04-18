using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Navigation
{
    public sealed class NavigationAddObstacleParams
    {
        public int? InstanceId { get; set; }
        public string Name { get; set; }
        [Required] public string Shape { get; set; } // "Box" or "Capsule"
        public float[] Size { get; set; }            // [x,y,z] for box or [radius,height] for capsule
        public bool? Carve { get; set; }
    }
}
