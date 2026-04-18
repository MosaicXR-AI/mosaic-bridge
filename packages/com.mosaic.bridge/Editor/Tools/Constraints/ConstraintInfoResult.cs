namespace Mosaic.Bridge.Tools.Constraints
{
    public sealed class ConstraintInfoResult
    {
        public string GameObjectName { get; set; }
        public ConstraintEntry[] Constraints { get; set; }
    }

    public sealed class ConstraintEntry
    {
        public string Type { get; set; }
        public string ComponentType { get; set; }
        public float Weight { get; set; }
        public bool IsActive { get; set; }
        public int SourceCount { get; set; }
        public string[] SourceNames { get; set; }
    }
}
