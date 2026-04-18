using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Reflection
{
    public static class ReflectionFindMethodTool
    {
        [MosaicTool("reflection/find-method",
                    "Lists methods on a type via reflection. Optionally filter by method name. " +
                    "Returns method signatures including parameters, return types, and modifiers.",
                    isReadOnly: true)]
        public static ToolResult<ReflectionFindMethodResult> Execute(ReflectionFindMethodParams p)
        {
            if (string.IsNullOrWhiteSpace(p.TypeName))
            {
                return ToolResult<ReflectionFindMethodResult>.Fail(
                    "TypeName is required.", ErrorCodes.INVALID_PARAM);
            }

            Type type = ResolveType(p.TypeName);
            if (type == null)
            {
                return ToolResult<ReflectionFindMethodResult>.Fail(
                    $"Type '{p.TypeName}' not found in loaded assemblies.",
                    ErrorCodes.NOT_FOUND);
            }

            try
            {
                BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
                if (p.IncludePrivate)
                    flags |= BindingFlags.NonPublic;

                MethodInfo[] methods = type.GetMethods(flags);

                if (!string.IsNullOrWhiteSpace(p.MethodName))
                {
                    methods = methods.Where(m =>
                        m.Name.Equals(p.MethodName, StringComparison.Ordinal)).ToArray();
                }

                var entries = methods.Select(m => new ReflectionFindMethodResult.MethodEntry
                {
                    Name = m.Name,
                    ReturnType = m.ReturnType.FullName ?? m.ReturnType.Name,
                    Parameters = m.GetParameters().Select(pi => new ReflectionFindMethodResult.ParameterEntry
                    {
                        Name = pi.Name,
                        Type = pi.ParameterType.FullName ?? pi.ParameterType.Name
                    }).ToList(),
                    IsStatic = m.IsStatic,
                    IsPublic = m.IsPublic,
                    DeclaringType = m.DeclaringType?.FullName ?? m.DeclaringType?.Name
                }).ToList();

                return ToolResult<ReflectionFindMethodResult>.Ok(new ReflectionFindMethodResult
                {
                    TypeName = type.FullName,
                    MethodCount = entries.Count,
                    Methods = entries
                });
            }
            catch (Exception ex)
            {
                return ToolResult<ReflectionFindMethodResult>.Fail(
                    $"Failed to enumerate methods: {ex.GetType().Name}: {ex.Message}",
                    ErrorCodes.INTERNAL_ERROR);
            }
        }

        internal static Type ResolveType(string typeName)
        {
            // Try direct resolution first
            Type t = Type.GetType(typeName);
            if (t != null) return t;

            // Search all loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(typeName);
                    if (t != null) return t;
                }
                catch
                {
                    // Skip assemblies that throw on GetType (e.g. dynamic assemblies)
                }
            }

            return null;
        }
    }
}
