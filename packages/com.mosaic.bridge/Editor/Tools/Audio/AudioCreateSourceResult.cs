namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioCreateSourceResult
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public string HierarchyPath { get; set; }
        public int ComponentInstanceId { get; set; }
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public float SpatialBlend { get; set; }
        public bool Loop { get; set; }
        public bool PlayOnAwake { get; set; }
        public string ClipName { get; set; }
    }
}
