using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Components
{
    public static class ComponentListTool
    {
        [MosaicTool("component/list",
                    "Lists all components attached to a GameObject, identified by InstanceId or name",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<ComponentListResult> Execute(ComponentListParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<ComponentListResult>.Fail(
                    "Either InstanceId or GameObjectName is required", ErrorCodes.INVALID_PARAM);

            GameObject go = null;

            if (p.InstanceId.HasValue)
            {
#pragma warning disable CS0618
                go = UnityEngine.Resources.EntityIdToObject(p.InstanceId.Value) as GameObject;
#pragma warning restore CS0618
            }

            if (go == null && !string.IsNullOrEmpty(p.GameObjectName))
                go = GameObject.Find(p.GameObjectName);

            if (go == null)
                return ToolResult<ComponentListResult>.Fail(
                    $"GameObject not found (InstanceId={p.InstanceId}, Name='{p.GameObjectName}')",
                    ErrorCodes.NOT_FOUND);

            var rawComponents = go.GetComponents<Component>();
            var allEntries = new List<ComponentEntry>(rawComponents.Length);
            foreach (var c in rawComponents)
            {
                if (c == null) continue;
                var t = c.GetType();
                allEntries.Add(new ComponentEntry
                {
                    TypeName           = t.Name,
                    FullTypeName       = t.FullName,
                    ComponentInstanceId = c.GetInstanceID()
                });
            }

            int totalCount = allEntries.Count;
            int offset = PaginationHelper.DecodeOffset(p.PageToken);
            var (page, nextToken) = PaginationHelper.Paginate(allEntries, offset, p.PageSize);

            return ToolResult<ComponentListResult>.Ok(new ComponentListResult
            {
                GameObjectName = go.name,
                InstanceId     = go.GetInstanceID(),
                Components     = page,
                TotalCount     = totalCount,
                NextPageToken  = nextToken
            });
        }
    }
}
