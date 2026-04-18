namespace Mosaic.Bridge.Tools.Cameras
{
    public sealed class CameraInfoResult
    {
        public CameraInfoEntry[] Cameras { get; set; }
    }

    public sealed class CameraInfoEntry
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public float FieldOfView { get; set; }
        public float NearClipPlane { get; set; }
        public float FarClipPlane { get; set; }
        public string ClearFlags { get; set; }
        public int CullingMask { get; set; }
        public float Depth { get; set; }
        public bool IsOrthographic { get; set; }
        public float OrthographicSize { get; set; }
        public float[] Position { get; set; }   // [x,y,z]
        public float[] Rotation { get; set; }   // euler [x,y,z]
        public bool IsMainCamera { get; set; }
    }
}
