using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Meta
{
    public class MetaRuntimeToolsResult
    {
        /// <summary>Total count of runtime-compatible tools.</summary>
        public int TotalCount { get; set; }

        /// <summary>Tools grouped by category.</summary>
        public List<RuntimeToolCategory> Categories { get; set; }
    }

    public class RuntimeToolCategory
    {
        public string Category { get; set; }
        public int Count { get; set; }
        public List<RuntimeToolInfo> Tools { get; set; }
    }

    public class RuntimeToolInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public bool IsReadOnly { get; set; }
        public string Context { get; set; }
    }
}
