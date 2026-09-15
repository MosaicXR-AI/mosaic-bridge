namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingReflectionProbeBakedInfo
    {
        public string Name { get; set; }
        public bool Success { get; set; }
        public string BakePath { get; set; }
    }

    public sealed class LightingReflectionProbeResult
    {
        public string Action { get; set; }
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string Mode { get; set; }
        public int Resolution { get; set; }
        public float Intensity { get; set; }
        public bool BakeSuccess { get; set; }
        public string BakePath { get; set; }
        public LightingReflectionProbeBakedInfo[] BakedProbes { get; set; }
    }
}
