namespace Mosaic.Bridge.Tools.Assets
{
    public sealed class AssetListParams
    {
        /// <summary>AssetDatabase filter string, e.g. "t:Texture2D" or "*.mat". Null returns all assets.</summary>
        public string Filter { get; set; }

        /// <summary>Folder to search within. Defaults to "Assets" when null.</summary>
        public string Path { get; set; }

        /// <summary>Opaque cursor returned by a previous response's nextPageToken. Omit for the first page.</summary>
        public string PageToken { get; set; }

        /// <summary>Number of results per page (1-200, default 50).</summary>
        public int PageSize { get; set; } = 0;
    }
}
