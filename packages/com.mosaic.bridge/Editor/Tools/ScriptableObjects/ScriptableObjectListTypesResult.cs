using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.ScriptableObjects
{
    public sealed class ScriptableObjectListTypesResult
    {
        public int Count { get; set; }
        public List<ScriptableObjectTypeInfo> Types { get; set; }
    }

    public sealed class ScriptableObjectTypeInfo
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Assembly { get; set; }
    }
}
