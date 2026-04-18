namespace Mosaic.Bridge.Tools.Search
{
    /// <summary>Parameters for the missing-references scan.</summary>
    public sealed class SearchMissingReferencesParams
    {
        /// <summary>Opaque cursor returned by a previous response's nextPageToken. Omit for the first page.</summary>
        public string PageToken { get; set; }

        /// <summary>Number of results per page (1-200, default 50).</summary>
        public int PageSize { get; set; } = 0;
    }
}
