using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphListResult
    {
        public List<ShaderGraphRef> Graphs     { get; set; }
        public int                  Count      { get; set; }
        public int                  TotalCount { get; set; }
        /// <summary>True when there were more results than the page size.</summary>
        public bool                 Truncated  { get; set; }
        /// <summary>Opaque cursor for the next page. Null when no more results.</summary>
        public string               NextPageToken { get; set; }
    }

    public sealed class ShaderGraphRef
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string Guid { get; set; }
    }
}
