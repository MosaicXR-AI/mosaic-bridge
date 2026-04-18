namespace Mosaic.Bridge.Tools.SceneViewCamera
{
    public sealed class SceneViewSetCameraParams
    {
        /// <summary>Camera position [x, y, z]. Null to leave unchanged.</summary>
        public float[] Position { get; set; }
        /// <summary>Camera euler rotation [x, y, z]. Null to leave unchanged.</summary>
        public float[] Rotation { get; set; }
        /// <summary>SceneView pivot point [x, y, z]. Null to leave unchanged.</summary>
        public float[] Pivot { get; set; }
        /// <summary>SceneView orbit size. Null to leave unchanged.</summary>
        public float? Size { get; set; }
        /// <summary>Whether the scene view uses orthographic projection. Null to leave unchanged.</summary>
        public bool? Orthographic { get; set; }
    }
}
