using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectCreateParams
    {
        [Required] public string Name { get; set; }
        public float[] Position { get; set; }  // [x,y,z] — null defaults to [0,0,0]
        public float[] Rotation { get; set; }  // euler angles [x,y,z] — null defaults to [0,0,0]
        public float[] Scale { get; set; }     // [x,y,z] — null defaults to [1,1,1]
        public string Parent { get; set; }     // find parent by name; null = scene root

        /// <summary>
        /// If set, creates a primitive with a built-in mesh + collider + renderer.
        /// Valid values: Cube, Sphere, Cylinder, Plane, Capsule, Quad.
        /// If null/empty, creates an empty GameObject.
        /// </summary>
        public string PrimitiveType { get; set; }
    }
}
