using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphInfoResult
    {
        public string       AssetPath      { get; set; }
        public string       Name           { get; set; }
        public string       Guid           { get; set; }
        public int          NodeCount      { get; set; }
        public int          EdgeCount      { get; set; }
        public int          PropertyCount  { get; set; }
        public List<string> PropertyNames  { get; set; }
    }
}
