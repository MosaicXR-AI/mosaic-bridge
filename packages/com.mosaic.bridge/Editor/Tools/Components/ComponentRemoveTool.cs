using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Components
{
    public static class ComponentRemoveTool
    {
        [MosaicTool("component/remove",
                    "Removes a component from a GameObject by fully-qualified or short type name",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ComponentRemoveResult> Remove(ComponentRemoveParams p)
        {
            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<ComponentRemoveResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var type = ResolveType(p.ComponentType);
            if (type == null)
                return ToolResult<ComponentRemoveResult>.Fail(
                    $"Component type not found: {p.ComponentType}", ErrorCodes.NOT_FOUND);

            if (type == typeof(Transform))
                return ToolResult<ComponentRemoveResult>.Fail(
                    "Cannot remove Transform component", ErrorCodes.NOT_PERMITTED);

            var component = go.GetComponent(type);
            if (component == null)
                return ToolResult<ComponentRemoveResult>.Fail(
                    $"Component '{p.ComponentType}' not found on '{p.GameObjectName}'", ErrorCodes.NOT_FOUND);

            Undo.DestroyObjectImmediate(component);

            return ToolResult<ComponentRemoveResult>.Ok(new ComponentRemoveResult
            {
                GameObjectName = go.name,
                ComponentType = type.FullName
            });
        }

        private static Type ResolveType(string typeName)
        {
            var t = Type.GetType(typeName);
            if (t != null) return t;
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(x => x.Name == typeName || x.FullName == typeName);
        }
    }
}
