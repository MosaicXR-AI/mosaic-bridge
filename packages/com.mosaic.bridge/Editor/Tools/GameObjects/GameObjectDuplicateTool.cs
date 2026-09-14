using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectDuplicateTool
    {
        [MosaicTool("gameobject/duplicate",
                    "Duplicates an existing GameObject. Optional params: NewName (explicit name, else auto-unique like 'Cube (1)'), Position (world-space [x,y,z]), Parent (name; empty string to unparent).",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<GameObjectCreateResult> Duplicate(GameObjectDuplicateParams p)
        {
            var source = GameObject.Find(p.Name);
            if (source == null)
                return ToolResult<GameObjectCreateResult>.Fail(
                    $"GameObject '{p.Name}' not found", ErrorCodes.NOT_FOUND);

            // L7: Object.Instantiate(source) produces a plain copy with no relationship to the
            // prefab asset — duplicating a prefab instance used to silently disconnect it.
            // Unsupported.DuplicateGameObjectsUsingPasteboard is the exact mechanism behind the
            // Editor's own Edit > Duplicate (Ctrl+D) command, so it keeps a prefab instance
            // connected exactly as dragging/Ctrl+D would, and needs no special-casing for plain
            // (non-prefab) GameObjects either. It also registers its own Undo step.
            var previousSelection = UnityEditor.Selection.objects;
            UnityEditor.Selection.activeGameObject = source;
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            var dupe = UnityEditor.Selection.activeGameObject;
            UnityEditor.Selection.objects = previousSelection;

            if (dupe == null || dupe == source)
                return ToolResult<GameObjectCreateResult>.Fail(
                    $"Duplicating '{p.Name}' failed — Unsupported.DuplicateGameObjectsUsingPasteboard did not " +
                    "produce a new selected GameObject.", ErrorCodes.INTERNAL_ERROR);

            // Name: explicit override wins; otherwise leave the name DuplicateGameObjectsUsingPasteboard
            // already assigned (it uniquifies exactly like the Editor's own Ctrl+D would).
            if (!string.IsNullOrEmpty(p.NewName))
            {
                dupe.name = p.NewName;
            }

            // Parent handling: DuplicateGameObjectsUsingPasteboard already places dupe as a
            // sibling under source's own parent, matching Ctrl+D — no action needed when Parent
            // is null. Empty string unparents. Non-empty string looks up by name; failure to
            // find is a hard error (silent attach-to-root would hide user intent).
            if (p.Parent != null)
            {
                if (p.Parent.Length == 0)
                {
                    dupe.transform.SetParent(null, worldPositionStays: false);
                }
                else
                {
                    var parent = GameObject.Find(p.Parent);
                    if (parent == null)
                    {
                        // Undo exactly the duplicate DuplicateGameObjectsUsingPasteboard just
                        // registered, rather than DestroyImmediate-ing an object still sitting in
                        // Unity's own Undo stack — that would leave a create record whose target
                        // no longer exists.
                        Undo.PerformUndo();
                        return ToolResult<GameObjectCreateResult>.Fail(
                            $"Parent GameObject '{p.Parent}' not found", ErrorCodes.NOT_FOUND);
                    }
                    dupe.transform.SetParent(parent.transform, worldPositionStays: false);
                }
            }

            // Position: optional world-space override. Applied AFTER parenting so the
            // world-space value resolves regardless of parent's transform.
            if (p.Position != null && p.Position.Length == 3)
            {
                dupe.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);
            }

            return ToolResult<GameObjectCreateResult>.Ok(new GameObjectCreateResult
            {
                InstanceId    = UnityIds.Of(dupe),
                Name          = dupe.name,
                HierarchyPath = GameObjectToolHelpers.GetHierarchyPath(dupe.transform)
            });
        }
    }
}
