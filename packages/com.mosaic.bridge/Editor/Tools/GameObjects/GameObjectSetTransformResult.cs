namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectSetTransformResult
    {
        public string Name { get; set; }
        public float[] Position { get; set; }   // world position [x,y,z]
        public float[] Rotation { get; set; }   // world euler angles [x,y,z]
        public float[] Scale { get; set; }      // local scale [x,y,z]
    }
}
