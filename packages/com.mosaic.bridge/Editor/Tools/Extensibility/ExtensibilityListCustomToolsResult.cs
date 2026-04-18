using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Extensibility
{
    /// <summary>
    /// Result envelope for the extensibility/list_custom_tools tool.
    /// </summary>
    public sealed class ExtensibilityListCustomToolsResult
    {
        /// <summary>List of discovered custom tools grouped by assembly.</summary>
        public List<CustomToolInfo> CustomTools { get; set; }

        /// <summary>Total number of custom tools discovered (before any filtering).</summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// Metadata for a single custom (non-built-in) tool discovered via [MosaicTool].
    /// </summary>
    public sealed class CustomToolInfo
    {
        /// <summary>Canonical tool name with mosaic_ prefix (e.g. "mosaic_custom_hello").</summary>
        public string Name { get; set; }

        /// <summary>Human-readable description from the [MosaicTool] attribute.</summary>
        public string Description { get; set; }

        /// <summary>Assembly name where the tool is declared.</summary>
        public string Assembly { get; set; }

        /// <summary>Fully qualified name of the declaring type.</summary>
        public string DeclaringType { get; set; }

        /// <summary>True if the tool is read-only (does not mutate Unity state).</summary>
        public bool IsReadOnly { get; set; }

        /// <summary>Tool category parsed from the route.</summary>
        public string Category { get; set; }
    }
}
