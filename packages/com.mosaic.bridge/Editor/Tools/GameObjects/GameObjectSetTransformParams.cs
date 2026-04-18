using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectSetTransformParams
    {
        /// <summary>Find by name. Either Name or InstanceId is required.</summary>
        public string Name { get; set; }
        /// <summary>Find by instance ID. Takes priority over Name if both set.</summary>
        public int? InstanceId { get; set; }
        public float[] Position { get; set; }   // [x,y,z] — null = no change
        public float[] Rotation { get; set; }   // [x,y,z] euler angles — null = no change
        public float[] Scale { get; set; }      // [x,y,z] — null = no change
        public string Space { get; set; } = "world";  // "world" | "local"
    }
}
