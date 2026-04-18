using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Assets
{
    public sealed class AssetListResult
    {
        public List<AssetInfo> Assets    { get; set; }
        public int             Count     { get; set; }
        public int             TotalCount { get; set; }
        /// <summary>True when there were more than 500 matches and the list was capped.</summary>
        public bool            Truncated { get; set; }
        /// <summary>Opaque cursor for the next page. Null when no more results.</summary>
        public string          NextPageToken { get; set; }
    }
}
