using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.ScriptableObjects
{
    public sealed class ScriptableObjectInfoResult
    {
        public string AssetPath { get; set; }
        public string TypeName { get; set; }
        public string Guid { get; set; }
        public List<ScriptableObjectFieldInfo> Fields { get; set; }
    }

    public sealed class ScriptableObjectFieldInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public object Value { get; set; }
    }
}
