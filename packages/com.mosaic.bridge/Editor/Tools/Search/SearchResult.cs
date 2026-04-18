namespace Mosaic.Bridge.Tools.Search
{
    /// <summary>Shared result DTO for scene-object search tools.</summary>
    public sealed class SearchResult
    {
        public int    InstanceId    { get; set; }
        public string Name          { get; set; }
        public string HierarchyPath { get; set; }
    }
}
