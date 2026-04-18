using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Reflection
{
    public static class ReflectionGetTypesTool
    {
        /// <summary>
        /// Maximum number of types returned to prevent unbounded responses.
        /// </summary>
        private const int MaxResults = 200;

        [MosaicTool("reflection/get-types",
                    "Searches loaded assemblies for types matching optional filters (assembly name, " +
                    "base type, type name). Returns type metadata including inheritance and modifiers.",
                    isReadOnly: true)]
        public static ToolResult<ReflectionGetTypesResult> Execute(ReflectionGetTypesParams p)
        {
            try
            {
                // Resolve base type filter if specified
                Type baseTypeFilter = null;
                if (!string.IsNullOrWhiteSpace(p.BaseType))
                {
                    baseTypeFilter = ReflectionFindMethodTool.ResolveType(p.BaseType);
                    if (baseTypeFilter == null)
                    {
                        return ToolResult<ReflectionGetTypesResult>.Fail(
                            $"Base type '{p.BaseType}' not found in loaded assemblies.",
                            ErrorCodes.NOT_FOUND);
                    }
                }

                var entries = new List<ReflectionGetTypesResult.TypeEntry>();
                bool hasAssemblyFilter = !string.IsNullOrWhiteSpace(p.AssemblyFilter);
                bool hasNameFilter = !string.IsNullOrWhiteSpace(p.NameFilter);

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Skip dynamic assemblies that cannot enumerate types
                    if (asm.IsDynamic) continue;

                    string asmName = asm.GetName().Name;

                    // Assembly filter: contains match
                    if (hasAssemblyFilter &&
                        asmName.IndexOf(p.AssemblyFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    Type[] types;
                    try
                    {
                        types = asm.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Some assemblies fail to load all types; use what we can
                        types = ex.Types.Where(t => t != null).ToArray();
                    }

                    foreach (var t in types)
                    {
                        // Name filter: contains match on FullName
                        if (hasNameFilter &&
                            (t.FullName == null ||
                             t.FullName.IndexOf(p.NameFilter, StringComparison.OrdinalIgnoreCase) < 0))
                        {
                            continue;
                        }

                        // Base type filter: must be assignable
                        if (baseTypeFilter != null && !baseTypeFilter.IsAssignableFrom(t))
                        {
                            continue;
                        }

                        // Skip the base type itself when filtering by base type
                        if (baseTypeFilter != null && t == baseTypeFilter)
                        {
                            continue;
                        }

                        entries.Add(new ReflectionGetTypesResult.TypeEntry
                        {
                            FullName = t.FullName,
                            AssemblyName = asmName,
                            BaseType = t.BaseType?.FullName,
                            IsAbstract = t.IsAbstract,
                            IsInterface = t.IsInterface,
                            IsEnum = t.IsEnum
                        });

                        if (entries.Count >= MaxResults) break;
                    }

                    if (entries.Count >= MaxResults) break;
                }

                var result = new ReflectionGetTypesResult
                {
                    TypeCount = entries.Count,
                    Types = entries
                };

                if (entries.Count >= MaxResults)
                {
                    return ToolResult<ReflectionGetTypesResult>.OkWithWarnings(result,
                        $"Results capped at {MaxResults}. Narrow your filters for complete results.");
                }

                return ToolResult<ReflectionGetTypesResult>.Ok(result);
            }
            catch (Exception ex)
            {
                return ToolResult<ReflectionGetTypesResult>.Fail(
                    $"Type search failed: {ex.GetType().Name}: {ex.Message}",
                    ErrorCodes.INTERNAL_ERROR);
            }
        }
    }
}
