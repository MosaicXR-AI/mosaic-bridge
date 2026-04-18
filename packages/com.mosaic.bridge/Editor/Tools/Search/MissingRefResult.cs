namespace Mosaic.Bridge.Tools.Search
{
    /// <summary>Describes a single missing object-reference found on a component.</summary>
    public sealed class MissingRefResult
    {
        public string GameObjectName { get; set; }
        public string HierarchyPath  { get; set; }
        public string ComponentType  { get; set; }
        public string PropertyName   { get; set; }
    }
}
