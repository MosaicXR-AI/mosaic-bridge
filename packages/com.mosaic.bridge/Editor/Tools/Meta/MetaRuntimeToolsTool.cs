using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using UnityEditor;

namespace Mosaic.Bridge.Tools.Meta
{
    /// <summary>
    /// Returns all tools that are runtime-compatible (Context == Runtime or Both),
    /// grouped by category. Useful for clients building runtime tool subsets.
    /// </summary>
    public static class MetaRuntimeToolsTool
    {
        [MosaicTool("meta/runtime_tools",
                    "Lists all tools that are safe to run in compiled builds (Context = Runtime or Both), grouped by category",
                    isReadOnly: true)]
        public static ToolResult<MetaRuntimeToolsResult> Execute(MetaRuntimeToolsParams p)
        {
            var methods = TypeCache.GetMethodsWithAttribute<MosaicToolAttribute>();
            var runtimeTools = new List<RuntimeToolInfo>();

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<MosaicToolAttribute>();
                if (attr == null) continue;

                if (attr.Context != ToolContext.Runtime && attr.Context != ToolContext.Both)
                    continue;

                // Apply optional category filter
                if (!string.IsNullOrEmpty(p?.Category) &&
                    !string.Equals(attr.Category, p.Category, StringComparison.OrdinalIgnoreCase))
                    continue;

                var toolName = "mosaic_" + attr.Route.Replace('/', '_');
                runtimeTools.Add(new RuntimeToolInfo
                {
                    Name = toolName,
                    Description = attr.Description,
                    Category = attr.Category,
                    IsReadOnly = attr.IsReadOnly,
                    Context = attr.Context.ToString().ToLowerInvariant()
                });
            }

            // Group by category, sorted
            var categories = runtimeTools
                .GroupBy(t => t.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new RuntimeToolCategory
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Tools = g.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList()
                })
                .ToList();

            return ToolResult<MetaRuntimeToolsResult>.Ok(new MetaRuntimeToolsResult
            {
                TotalCount = runtimeTools.Count,
                Categories = categories
            });
        }
    }
}
