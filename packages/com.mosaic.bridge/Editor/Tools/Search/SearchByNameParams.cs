namespace Mosaic.Bridge.Tools.Search
{
    public sealed class SearchByNameParams
    {
        public string Pattern        { get; set; }
        public bool   UseRegex       { get; set; } = false;
        public bool   IncludeInactive { get; set; } = true;
        public int    MaxResults     { get; set; } = 100;

        /// <summary>Opaque cursor returned by a previous response's nextPageToken. Omit for the first page.</summary>
        public string PageToken { get; set; }

        /// <summary>Number of results per page (1-200, default 50).</summary>
        public int PageSize { get; set; } = 0;
    }
}
