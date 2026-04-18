namespace Mosaic.Bridge.Tools.Constraints
{
    public sealed class ConstraintAddResult
    {
        public string GameObjectName { get; set; }
        public string ConstraintType { get; set; }
        public string ComponentType { get; set; }
        public bool SourceAssigned { get; set; }
    }
}
