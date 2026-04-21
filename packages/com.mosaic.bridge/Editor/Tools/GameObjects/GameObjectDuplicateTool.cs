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
                    "Duplicates an existing GameObject. Optional params: NewName (explicit name, else auto-unique like 'Cube (1)'), Position (world-space [x,y,z]), Parent (name; empty string to unparent).",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<GameObjectCreateResult> Duplicate(GameObjectDuplicateParams p)
        {
            var source = GameObject.Find(p.Name);
            if (source == null)
                return ToolResult<GameObjectCreateResult>.Fail(
                    $"GameObject '{p.Name}' not found", ErrorCodes.NOT_FOUND);

            var dupe = UnityEngine.Object.Instantiate(source);

            // Name: explicit override wins; otherwise Unity's default " (N)" uniquifier
            // which mimics editor behavior via GameObjectUtility.
            if (!string.IsNullOrEmpty(p.NewName))
            {
                dupe.name = p.NewName;
            }
            else
            {
                // Ensure name uniqueness against siblings so downstream finds unambiguous.
                dupe.name = GameObjectUtility.GetUniqueNameForSibling(
                    source.transform.parent, source.name);
            }

            // Parent handling: null preserves source's parent. Empty string unparents.
            // Non-empty string looks up by name; failure to find is a hard error
            // (silent attach-to-root would hide user intent).
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
                        UnityEngine.Object.DestroyImmediate(dupe);
                        return ToolResult<GameObjectCreateResult>.Fail(
                            $"Parent GameObject '{p.Parent}' not found", ErrorCodes.NOT_FOUND);
                    }
                    dupe.transform.SetParent(parent.transform, worldPositionStays: false);
                }
            }
            else
            {
                dupe.transform.SetParent(source.transform.parent, worldPositionStays: false);
            }

            // Position: optional world-space override. Applied AFTER parenting so the
            // world-space value resolves regardless of parent's transform.
            if (p.Position != null && p.Position.Length == 3)
            {
                dupe.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);
            }

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
