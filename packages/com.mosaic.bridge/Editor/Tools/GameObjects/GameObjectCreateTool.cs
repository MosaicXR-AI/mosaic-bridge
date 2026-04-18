using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectCreateTool
    {
        [MosaicTool("gameobject/create",
                    "Creates a new GameObject in the currently open scene",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<GameObjectCreateResult> Create(GameObjectCreateParams p)
        {
            // 1. Create: either primitive (with mesh/renderer/collider) or empty
            GameObject go;
            if (!string.IsNullOrEmpty(p.PrimitiveType))
            {
                if (!System.Enum.TryParse<PrimitiveType>(p.PrimitiveType, true, out var primType))
                    return ToolResult<GameObjectCreateResult>.Fail(
                        $"Invalid PrimitiveType '{p.PrimitiveType}'. Valid: Cube, Sphere, Cylinder, Plane, Capsule, Quad",
                        ErrorCodes.INVALID_PARAM);
                go = GameObject.CreatePrimitive(primType);
                go.name = p.Name;
            }
            else
            {
                go = new GameObject(p.Name);
            }

            // 2. Position
            if (p.Position != null && p.Position.Length == 3)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            // 3. Rotation
            if (p.Rotation != null && p.Rotation.Length == 3)
                go.transform.eulerAngles = new Vector3(p.Rotation[0], p.Rotation[1], p.Rotation[2]);

            // 3b. Scale
            if (p.Scale != null && p.Scale.Length == 3)
                go.transform.localScale = new Vector3(p.Scale[0], p.Scale[1], p.Scale[2]);

            // 4. Parent
            if (!string.IsNullOrEmpty(p.Parent))
            {
                var parent = GameObject.Find(p.Parent);
                if (parent == null)
                    return ToolResult<GameObjectCreateResult>.Fail(
                        $"Parent GameObject '{p.Parent}' not found", ErrorCodes.NOT_FOUND);
                go.transform.SetParent(parent.transform, worldPositionStays: true);
            }

            // 5. Register with Undo so the user can Ctrl+Z the AI action
            // Must be called AFTER setting parent/transform
            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create GameObject");

            return ToolResult<GameObjectCreateResult>.Ok(new GameObjectCreateResult
            {
                InstanceId    = go.GetInstanceID(),
                Name          = go.name,
                HierarchyPath = GetHierarchyPath(go.transform)
            });
        }

        private static string GetHierarchyPath(Transform t)
        {
            var path = t.name;
            var current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
