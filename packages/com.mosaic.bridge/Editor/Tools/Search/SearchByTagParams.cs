using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Search
{
    public sealed class SearchByTagParams
    {
        [Required] public string Tag { get; set; }

        /// <summary>Opaque cursor returned by a previous response's nextPageToken. Omit for the first page.</summary>
        public string PageToken { get; set; }

        /// <summary>Number of results per page (1-200, default 50).</summary>
        public int PageSize { get; set; } = 0;
    }
}
