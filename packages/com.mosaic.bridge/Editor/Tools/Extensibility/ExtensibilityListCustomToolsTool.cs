using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using UnityEditor;

namespace Mosaic.Bridge.Tools.Extensibility
{
    /// <summary>
    /// Lists all custom (non-built-in) tools discovered via [MosaicTool].
    /// Custom tools are those whose declaring type is NOT in the Mosaic.Bridge.Tools namespace.
    /// Results are grouped by assembly name.
    /// </summary>
    public static class ExtensibilityListCustomToolsTool
    {
        private const string BuiltInNamespace = "Mosaic.Bridge.Tools";

        [MosaicTool("extensibility/list_custom_tools",
                    "Lists all custom (non-built-in) tools discovered via [MosaicTool], grouped by assembly",
                    isReadOnly: true)]
        public static ToolResult<ExtensibilityListCustomToolsResult> ListCustomTools(
            ExtensibilityListCustomToolsParams p)
        {
            var methods = TypeCache.GetMethodsWithAttribute<MosaicToolAttribute>();

            var customTools = new List<CustomToolInfo>();

            foreach (var method in methods)
            {
                var declaringType = method.DeclaringType;
                if (declaringType == null) continue;

                // Skip built-in tools: any type whose namespace starts with Mosaic.Bridge.Tools
                var ns = declaringType.Namespace ?? "";
                if (ns.StartsWith(BuiltInNamespace, StringComparison.Ordinal))
                    continue;

                var attr = method.GetCustomAttribute<MosaicToolAttribute>();
                if (attr == null) continue;

                var assemblyName = declaringType.Assembly.GetName().Name ?? "";
                var toolName = "mosaic_" + attr.Route.Replace('/', '_');

                customTools.Add(new CustomToolInfo
                {
                    Name = toolName,
                    Description = attr.Description,
                    Assembly = assemblyName,
                    DeclaringType = declaringType.FullName,
                    IsReadOnly = attr.IsReadOnly,
                    Category = attr.Category
                });
            }

            var totalCount = customTools.Count;

            // Apply optional assembly filter
            if (!string.IsNullOrEmpty(p?.AssemblyFilter))
            {
                customTools = customTools
                    .Where(t => t.Assembly.IndexOf(p.AssemblyFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            // Sort by assembly then by name for deterministic output
            customTools = customTools
                .OrderBy(t => t.Assembly, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return ToolResult<ExtensibilityListCustomToolsResult>.Ok(
                new ExtensibilityListCustomToolsResult
                {
                    CustomTools = customTools,
                    TotalCount = totalCount
                });
        }
    }
}
