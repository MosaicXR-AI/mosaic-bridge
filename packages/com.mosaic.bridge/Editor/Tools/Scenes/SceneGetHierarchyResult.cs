using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Scenes
{
    public sealed class SceneGetHierarchyResult
    {
        public List<HierarchyNode> Roots { get; set; }
        public int TotalCount { get; set; }
        /// <summary>Opaque cursor for the next page. Null when no more results.</summary>
        public string NextPageToken { get; set; }
    }
}
