using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectRenameTool
    {
        [MosaicTool("gameobject/rename",
                    "Renames a GameObject in the currently open scene. " +
                    "Works on inactive GameObjects too. Refuses when the name is already taken, " +
                    "because two objects sharing a name make every later find-by-name ambiguous.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<GameObjectRenameResult> Rename(GameObjectRenameParams p)
        {
            if (string.IsNullOrWhiteSpace(p.NewName))
                return ToolResult<GameObjectRenameResult>.Fail(
                    "newName must not be empty — an unnamed GameObject cannot be found again",
                    ErrorCodes.INVALID_PARAM);

            // Resources.FindObjectsOfTypeAll finds BOTH active and inactive scene objects; a scene
            // search that skips inactive ones would refuse to rename something plainly there.
            GameObject go = null;
            var newNameTaken = false;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
                    continue;   // prefab assets live outside any scene
                if (go == null && candidate.name == p.Name)
                    go = candidate;
                else if (candidate.name == p.NewName)
                    newNameTaken = true;
            }

            if (go == null)
                return ToolResult<GameObjectRenameResult>.Fail(
                    $"GameObject '{p.Name}' not found", ErrorCodes.NOT_FOUND);

            if (p.Name == p.NewName)
                return ToolResult<GameObjectRenameResult>.Ok(new GameObjectRenameResult
                {
                    PreviousName = p.Name,
                    Name = go.name
                });

            // Refusing a duplicate is not fussiness. Every other tool here addresses objects BY
            // NAME, so allowing two to share one makes the next find-by-name pick an arbitrary
            // winner — and a course step that renames onto an existing name would silently start
            // operating on the wrong object.
            if (newNameTaken)
                return ToolResult<GameObjectRenameResult>.Fail(
                    $"A GameObject named '{p.NewName}' already exists in this scene. " +
                    "Renaming onto it would make every later lookup by that name ambiguous.",
                    ErrorCodes.CONFLICT);

            var previous = go.name;
            Undo.RecordObject(go, "Mosaic: Rename GameObject");
            go.name = p.NewName;
            EditorUtility.SetDirty(go);

            return ToolResult<GameObjectRenameResult>.Ok(new GameObjectRenameResult
            {
                PreviousName = previous,
                Name = go.name
            });
        }
    }
}
