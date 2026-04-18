using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectReparentTool
    {
        [MosaicTool("gameobject/reparent",
                    "Changes the parent of a GameObject; pass null NewParent to move to scene root",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<GameObjectReparentResult> Reparent(GameObjectReparentParams p)
        {
            var go = GameObject.Find(p.Name);
            if (go == null)
                return ToolResult<GameObjectReparentResult>.Fail(
                    $"GameObject '{p.Name}' not found", ErrorCodes.NOT_FOUND);

            GameObject parent = null;

            if (!string.IsNullOrEmpty(p.NewParent))
            {
                parent = GameObject.Find(p.NewParent);
                if (parent == null)
                    return ToolResult<GameObjectReparentResult>.Fail(
                        $"Parent GameObject '{p.NewParent}' not found", ErrorCodes.NOT_FOUND);

                if (parent == go)
                    return ToolResult<GameObjectReparentResult>.Fail(
                        "A GameObject cannot be its own parent", ErrorCodes.INVALID_PARAM);
            }

            Undo.SetTransformParent(go.transform, parent?.transform, "Mosaic: Reparent");

            return ToolResult<GameObjectReparentResult>.Ok(new GameObjectReparentResult
            {
                Name             = go.name,
                NewHierarchyPath = GameObjectToolHelpers.GetHierarchyPath(go.transform),
                ParentName       = parent?.name ?? "(scene root)"
            });
        }
    }
}
