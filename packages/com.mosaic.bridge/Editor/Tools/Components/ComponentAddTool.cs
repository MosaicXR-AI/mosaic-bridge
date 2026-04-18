using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Components
{
    public static class ComponentAddTool
    {
        [MosaicTool("component/add",
                    "Adds a component to a GameObject by fully-qualified or short type name",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ComponentAddResult> Add(ComponentAddParams p)
        {
            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<ComponentAddResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var type = ResolveType(p.ComponentType);
            if (type == null)
                return ToolResult<ComponentAddResult>.Fail(
                    $"Component type not found: {p.ComponentType}", ErrorCodes.NOT_FOUND);

            var alreadyExisted = go.GetComponent(type) != null;
            Undo.AddComponent(go, type);

            return ToolResult<ComponentAddResult>.Ok(new ComponentAddResult
            {
                GameObjectName = go.name,
                ComponentType = type.FullName,
                AlreadyExisted = alreadyExisted
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
