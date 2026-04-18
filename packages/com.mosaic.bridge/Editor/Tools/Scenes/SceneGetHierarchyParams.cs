namespace Mosaic.Bridge.Tools.Scenes
{
    public sealed class SceneGetHierarchyParams
    {
        public int? MaxDepth { get; set; }
        public bool IncludeInactive { get; set; } = true;

        /// <summary>Opaque cursor returned by a previous response's nextPageToken. Omit for the first page.</summary>
        public string PageToken { get; set; }

        /// <summary>Number of root-level nodes per page (1-200, default 50).</summary>
        public int PageSize { get; set; } = 0;
    }
}
