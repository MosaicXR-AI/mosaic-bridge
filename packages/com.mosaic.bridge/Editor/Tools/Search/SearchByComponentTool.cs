using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Search
{
    public static class SearchByComponentTool
    {
        [MosaicTool("search/by_component",
                    "Finds all scene GameObjects that have a component of the given type",
                    isReadOnly: true)]
        public static ToolResult<SearchByComponentResult> Execute(SearchByComponentParams p)
        {
            var type = ResolveType(p.ComponentType);
            if (type == null)
                return ToolResult<SearchByComponentResult>.Fail(
                    $"Component type '{p.ComponentType}' could not be resolved", ErrorCodes.NOT_FOUND);

            var allMatches = Resources.FindObjectsOfTypeAll(type)
                .OfType<Component>()
                .Select(c => c.gameObject)
                .Distinct()
                .Where(go => go.scene.IsValid())
                .Select(go => new SearchResult
                {
                    InstanceId    = go.GetInstanceID(),
                    Name          = go.name,
                    HierarchyPath = GetHierarchyPath(go.transform)
                })
                .ToList();

            int totalCount = allMatches.Count;
            int offset = PaginationHelper.DecodeOffset(p.PageToken);
            var (page, nextToken) = PaginationHelper.Paginate(allMatches, offset, p.PageSize);

            return ToolResult<SearchByComponentResult>.Ok(new SearchByComponentResult
            {
                Matches       = page,
                Count         = page.Count,
                TotalCount    = totalCount,
                NextPageToken = nextToken
            });
        }

        private static readonly string[] s_commonNamespaces = new[]
        {
            "UnityEngine", "UnityEngine.UI", "UnityEngine.AI",
            "UnityEngine.Rendering", "UnityEngine.Rendering.Universal",
            "UnityEditor", "TMPro"
        };

        internal static Type ResolveType(string typeName)
        {
            // Try exact name first
            var type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            // Try common Unity namespaces for short names (e.g., "Rigidbody" → "UnityEngine.Rigidbody")
            if (!typeName.Contains("."))
            {
                foreach (var ns in s_commonNamespaces)
                {
                    string qualifiedName = ns + "." + typeName;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(qualifiedName);
                        if (type != null) return type;
                    }
                }
            }

            return null;
        }

        private static string GetHierarchyPath(Transform t)
        {
            var path    = t.name;
            var current = t.parent;
            while (current != null)
            {
                path    = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
