namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingBakeStatusResult
    {
        public bool IsRunning { get; set; }

        /// <summary>0..1. Only meaningful while IsRunning — Lightmapping.buildProgress is 0 when idle,
        /// whether that's because nothing has ever baked or because a bake just finished.</summary>
        public float Progress { get; set; }

        /// <summary>Durable evidence a bake actually produced data, independent of IsRunning — the
        /// same "check what's really on disk, not just the in-memory flag" rule the Job Registry
        /// (L15) uses for package/add.</summary>
        public int LightmapCount { get; set; }

        public string Message { get; set; }
    }
}
