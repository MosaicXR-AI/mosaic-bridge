using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphGetPropertiesResult
    {
        public string                     AssetPath   { get; set; }
        public List<ShaderGraphProperty>  Properties  { get; set; }
        public int                        Count       { get; set; }
    }

    public sealed class ShaderGraphProperty
    {
        public string Name         { get; set; }
        public string ReferenceName { get; set; }
        public string Type         { get; set; }
        public string DefaultValue { get; set; }
    }
}
