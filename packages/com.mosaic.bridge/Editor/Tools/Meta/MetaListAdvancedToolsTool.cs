using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Meta
{
    public static class MetaListAdvancedToolsTool
    {
        private static readonly HashSet<string> CoreCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gameobject", "component", "scene", "asset", "search", "script",
            "prefab", "material", "console", "selection", "undo", "settings",
            "editor", "camera"
        };

        [MosaicTool("meta/list_advanced_tools",
                    "Lists all advanced (non-core) tools grouped by category. Use meta/advanced_tool to invoke them.",
                    isReadOnly: true)]
        public static ToolResult<MetaListAdvancedToolsResult> Execute(MetaListAdvancedToolsParams p)
        {
            var methods = UnityEditor.TypeCache.GetMethodsWithAttribute<MosaicToolAttribute>();
            var advanced = new List<(string category, string name, string desc, bool readOnly, Type paramType)>();

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<MosaicToolAttribute>();
                if (attr == null) continue;

                var category = attr.Category ?? attr.Route?.Split('/')[0] ?? "unknown";
                if (CoreCategories.Contains(category)) continue;

                if (!string.IsNullOrEmpty(p?.Category) &&
                    !category.Contains(p.Category, StringComparison.OrdinalIgnoreCase))
                    continue;

                var paramType = method.GetParameters().Length > 0 ? method.GetParameters()[0].ParameterType : null;
                advanced.Add((category, attr.Route, attr.Description, attr.IsReadOnly, paramType));
            }

            var grouped = advanced
                .GroupBy(t => t.category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key)
                .Select(g => new MetaListAdvancedToolsResult.CategoryGroup
                {
                    Category = g.Key,
                    ToolCount = g.Count(),
                    Tools = g.OrderBy(t => t.name).Select(t => new MetaListAdvancedToolsResult.ToolSummary
                    {
                        Name = t.name,
                        Description = t.desc,
                        IsReadOnly = t.readOnly,
                        ParameterSummary = BuildParamSummary(t.paramType)
                    }).ToList()
                }).ToList();

            return ToolResult<MetaListAdvancedToolsResult>.Ok(new MetaListAdvancedToolsResult
            {
                TotalAdvancedTools = advanced.Count,
                CategoryCount = grouped.Count,
                Categories = grouped
            });
        }

        private static string BuildParamSummary(Type paramType)
        {
            if (paramType == null) return "(none)";
            var props = paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (props.Length == 0) return "(none)";

            var parts = props.Take(5).Select(prop =>
            {
                var typeName = prop.PropertyType.Name;
                if (prop.PropertyType == typeof(string)) typeName = "string";
                else if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(int?)) typeName = "int";
                else if (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(float?)) typeName = "float";
                else if (prop.PropertyType == typeof(bool)) typeName = "bool";
                else if (prop.PropertyType == typeof(float[])) typeName = "float[]";
                return $"{prop.Name}: {typeName}";
            });

            var summary = string.Join(", ", parts);
            if (props.Length > 5) summary += $", ... (+{props.Length - 5} more)";
            return summary;
        }
    }
}
