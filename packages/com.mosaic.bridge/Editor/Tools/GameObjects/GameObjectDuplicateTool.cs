using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectDuplicateTool
    {
        [MosaicTool("gameobject/duplicate",
                    "Duplicates an existing GameObject in the currently open scene",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<GameObjectCreateResult> Duplicate(GameObjectDuplicateParams p)
        {
            var source = GameObject.Find(p.Name);
            if (source == null)
                return ToolResult<GameObjectCreateResult>.Fail(
                    $"GameObject '{p.Name}' not found", ErrorCodes.NOT_FOUND);

            var dupe = UnityEngine.Object.Instantiate(source);
            dupe.name = source.name;

            Undo.RegisterCreatedObjectUndo(dupe, "Mosaic: Duplicate GameObject");

            return ToolResult<GameObjectCreateResult>.Ok(new GameObjectCreateResult
            {
                InstanceId    = dupe.GetInstanceID(),
                Name          = dupe.name,
                HierarchyPath = GameObjectToolHelpers.GetHierarchyPath(dupe.transform)
            });
        }
    }
}
