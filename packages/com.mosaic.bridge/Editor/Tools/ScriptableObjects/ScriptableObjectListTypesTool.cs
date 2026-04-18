using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.ScriptableObjects
{
    public static class ScriptableObjectListTypesTool
    {
        [MosaicTool("scriptableobject/list-types",
                    "Lists all ScriptableObject subclasses available in the project",
                    isReadOnly: true)]
        public static ToolResult<ScriptableObjectListTypesResult> Execute(ScriptableObjectListTypesParams p)
        {
            var allTypes = TypeCache.GetTypesDerivedFrom<ScriptableObject>();

            IEnumerable<Type> filtered = allTypes
                .Where(t => !t.IsAbstract && !t.IsGenericType);

            if (!string.IsNullOrEmpty(p.Filter))
            {
                var filter = p.Filter;
                filtered = filtered.Where(t =>
                    t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            var types = filtered
                .OrderBy(t => t.FullName)
                .Select(t => new ScriptableObjectTypeInfo
                {
                    Name     = t.Name,
                    FullName = t.FullName,
                    Assembly = t.Assembly.GetName().Name
                })
                .ToList();

            return ToolResult<ScriptableObjectListTypesResult>.Ok(new ScriptableObjectListTypesResult
            {
                Count = types.Count,
                Types = types
            });
        }
    }
}
