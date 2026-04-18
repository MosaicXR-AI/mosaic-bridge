using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectGetInfoTool
    {
        [MosaicTool("gameobject/get_info",
                    "Returns detailed information about a GameObject in the currently open scene",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<GameObjectGetInfoResult> GetInfo(GameObjectGetInfoParams p)
        {
            var go = GameObject.Find(p.Name);
            if (go == null)
                return ToolResult<GameObjectGetInfoResult>.Fail(
                    $"GameObject '{p.Name}' not found", ErrorCodes.NOT_FOUND);

            var rawComponents = go.GetComponents<Component>();
            var componentNames = new List<string>(rawComponents.Length);
            foreach (var c in rawComponents)
            {
                if (c != null)
                    componentNames.Add(c.GetType().Name);
            }

            return ToolResult<GameObjectGetInfoResult>.Ok(new GameObjectGetInfoResult
            {
                InstanceId        = go.GetInstanceID(),
                Name              = go.name,
                HierarchyPath     = GameObjectToolHelpers.GetHierarchyPath(go.transform),
                ActiveSelf        = go.activeSelf,
                ActiveInHierarchy = go.activeInHierarchy,
                Components        = componentNames.ToArray(),
                Tag               = go.tag,
                Layer             = LayerMask.LayerToName(go.layer),
                ChildCount        = go.transform.childCount
            });
        }
    }
}
