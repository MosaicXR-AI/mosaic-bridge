using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Meta
{
    public sealed class MetaListAdvancedToolsResult
    {
        public int TotalAdvancedTools { get; set; }
        public int CategoryCount { get; set; }
        public List<CategoryGroup> Categories { get; set; }

        public sealed class CategoryGroup
        {
            public string Category { get; set; }
            public int ToolCount { get; set; }
            public List<ToolSummary> Tools { get; set; }
        }

        public sealed class ToolSummary
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string ParameterSummary { get; set; }
            public bool IsReadOnly { get; set; }
        }
    }
}
