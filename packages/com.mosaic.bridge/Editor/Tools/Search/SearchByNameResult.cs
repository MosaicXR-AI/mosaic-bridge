using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Search
{
    public sealed class SearchByNameResult
    {
        public List<SearchResult> Matches    { get; set; }
        public int                TotalFound { get; set; }
        public bool               Truncated  { get; set; }
        /// <summary>Opaque cursor for the next page. Null when no more results.</summary>
        public string             NextPageToken { get; set; }
    }
}
