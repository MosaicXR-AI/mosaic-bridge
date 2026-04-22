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
                    "Sets the active state of a GameObject in the currently open scene. " +
                    "Works on both active and inactive GameObjects (unlike scene searches that skip inactive objects).",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<GameObjectSetActiveResult> SetActive(GameObjectSetActiveParams p)
        {
            // Resources.FindObjectsOfTypeAll finds BOTH active and inactive scene objects.
            // Filter to scene objects only (exclude prefab assets that live outside any scene).
            GameObject go = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name == p.Name
                    && candidate.scene.IsValid()
                    && candidate.scene.isLoaded)
                {
                    go = candidate;
                    break;
                }
            }

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
