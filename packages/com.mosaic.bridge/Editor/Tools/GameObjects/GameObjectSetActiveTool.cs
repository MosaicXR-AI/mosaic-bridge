using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectSetActiveTool
    {
        [MosaicTool("gameobject/set_active",
                    "Sets the active state of a GameObject in the currently open scene",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<GameObjectSetActiveResult> SetActive(GameObjectSetActiveParams p)
        {
            var go = GameObject.Find(p.Name);
            if (go == null)
                return ToolResult<GameObjectSetActiveResult>.Fail(
                    $"GameObject '{p.Name}' not found", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(go, "Mosaic: Set Active");
            go.SetActive(p.Active);

            return ToolResult<GameObjectSetActiveResult>.Ok(new GameObjectSetActiveResult
            {
                Name   = go.name,
                Active = go.activeSelf
            });
        }
    }
}
