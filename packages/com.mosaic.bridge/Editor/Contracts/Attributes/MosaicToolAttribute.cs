using System;

namespace Mosaic.Bridge.Contracts.Attributes
{
    /// <summary>
    /// Marks a static method as a Mosaic Bridge tool. The method is auto-discovered
    /// at bridge startup via Unity's TypeCache and registered as an MCP-callable tool.
    /// </summary>
    /// <remarks>
    /// Per FR13 (auto-discovery via TypeCache.GetMethodsWithAttribute) and FR15 (ToolResult envelope),
    /// methods marked with this attribute MUST:
    /// - Be static
    /// - Take a single typed parameter class as argument
    /// - Return a ToolResult&lt;T&gt; (or ToolResult&lt;PaginatedResult&lt;T&gt;&gt; for list operations)
    /// - Run on the Unity main thread (the dispatcher guarantees this)
    /// - Register Undo operations for any state changes (per FR7)
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class MosaicToolAttribute : Attribute
    {
        /// <summary>
        /// Canonical tool route in the format "category/action" (lowercase, kebab-case for multi-word actions).
        /// Examples: "gameobject/create", "component/set-property", "search/find-missing-references"
        /// </summary>
        public string Route { get; }

        /// <summary>
        /// Human-readable description shown in MCP tools/list responses and documentation.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// True if the tool only reads state (does not mutate the Unity project).
        /// Read-only tools can be batched and executed in parallel by the dispatcher (FR19).
        /// Default is false (assume mutating).
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// Optional category override. If not specified, the category is parsed from the Route
        /// (the part before the first slash).
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Declares the runtime compatibility context for this tool.
        /// Default is Editor (backward compatible — existing tools that don't specify context are editor-only).
        /// Set to ToolContext.Both for tools that use no editor-only APIs and can run in compiled builds.
        /// </summary>
        public ToolContext Context { get; set; } = ToolContext.Editor;

        public MosaicToolAttribute(string route, string description, bool isReadOnly = false, string category = null)
        {
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("Route cannot be null or empty.", nameof(route));
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Description cannot be null or empty.", nameof(description));
            if (!route.Contains("/"))
                throw new ArgumentException($"Route '{route}' must contain a category/action separator (e.g., 'gameobject/create').", nameof(route));

            Route = route;
            Description = description;
            IsReadOnly = isReadOnly;
            Category = category ?? route.Substring(0, route.IndexOf('/'));
        }
    }
}
