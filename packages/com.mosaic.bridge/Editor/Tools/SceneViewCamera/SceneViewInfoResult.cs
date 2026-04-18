namespace Mosaic.Bridge.Tools.SceneViewCamera
{
    public sealed class SceneViewInfoResult
    {
        public float[] Position { get; set; }
        public float[] Rotation { get; set; }
        public float[] Pivot { get; set; }
        public float Size { get; set; }
        public float FieldOfView { get; set; }
        public bool Orthographic { get; set; }
    }
}
