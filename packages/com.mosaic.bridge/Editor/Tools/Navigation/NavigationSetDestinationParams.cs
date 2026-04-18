using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Navigation
{
    public sealed class NavigationSetDestinationParams
    {
        public int? InstanceId { get; set; }
        public string Name { get; set; }
        [Required] public float[] Destination { get; set; } // [x,y,z]
    }
}
