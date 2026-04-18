namespace Mosaic.Bridge.Tools.SceneViewCamera
{
    public sealed class SceneViewSetCameraResult
    {
        public float[] Position { get; set; }
        public float[] Rotation { get; set; }
        public float[] Pivot { get; set; }
        public float Size { get; set; }
        public bool Orthographic { get; set; }
    }
}
