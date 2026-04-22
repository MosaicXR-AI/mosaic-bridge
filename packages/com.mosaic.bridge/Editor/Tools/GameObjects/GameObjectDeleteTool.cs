using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectDeleteTool
    {
        [MosaicTool("gameobject/delete",
                    "Deletes a GameObject from the currently open scene by name or instance ID",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<GameObjectDeleteResult> Delete(GameObjectDeleteParams p)
        {
            if (string.IsNullOrEmpty(p.Name) && !p.InstanceId.HasValue)
                return ToolResult<GameObjectDeleteResult>.Fail(
                    "Either Name or InstanceId must be provided", ErrorCodes.INVALID_PARAM);

            GameObject go = null;

            if (p.InstanceId.HasValue && p.InstanceId.Value != 0)
            {
#pragma warning disable CS0618
                go = UnityEngine.Resources.EntityIdToObject(p.InstanceId.Value) as GameObject;
#pragma warning restore CS0618
            }

            if (go == null && !string.IsNullOrEmpty(p.Name))
            {
                go = GameObject.Find(p.Name);
            }

            if (go == null)
                return ToolResult<GameObjectDeleteResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            var result = new GameObjectDeleteResult
            {
                Name       = go.name,
                InstanceId = go.GetInstanceID()
            };

            Undo.DestroyObjectImmediate(go);

            return ToolResult<GameObjectDeleteResult>.Ok(result);
        }
    }
}
