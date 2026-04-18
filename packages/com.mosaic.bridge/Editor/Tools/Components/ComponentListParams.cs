namespace Mosaic.Bridge.Tools.Components
{
    public sealed class ComponentListParams
    {
        public int?   InstanceId      { get; set; }
        public string GameObjectName  { get; set; }

        /// <summary>Opaque cursor returned by a previous response's nextPageToken. Omit for the first page.</summary>
        public string PageToken { get; set; }

        /// <summary>Number of results per page (1-200, default 50).</summary>
        public int PageSize { get; set; } = 0;
    }
}
