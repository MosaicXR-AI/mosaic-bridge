using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Search
{
    public sealed class SearchMissingReferencesResult
    {
        public List<MissingRefResult> Issues { get; set; }
        public int Count { get; set; }
        public int TotalCount { get; set; }
        /// <summary>Opaque cursor for the next page. Null when no more results.</summary>
        public string NextPageToken { get; set; }
    }
}
