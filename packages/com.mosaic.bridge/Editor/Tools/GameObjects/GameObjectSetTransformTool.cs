using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public static class GameObjectSetTransformTool
    {
        [MosaicTool("gameobject/set_transform",
                    "Sets the position, rotation, and/or scale of a GameObject",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<GameObjectSetTransformResult> SetTransform(GameObjectSetTransformParams p)
        {
            GameObject go = null;
            if (p.InstanceId.HasValue && p.InstanceId.Value != 0)
                go = UnityEngine.Resources.EntityIdToObject(p.InstanceId.Value) as GameObject;
            if (go == null && !string.IsNullOrEmpty(p.Name))
                go = GameObject.Find(p.Name);
            if (go == null)
                return ToolResult<GameObjectSetTransformResult>.Fail(
                    $"GameObject not found (InstanceId={p.InstanceId}, Name='{p.Name}')", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(go.transform, "Mosaic: Set Transform");

            bool isLocal = p.Space == "local";

            if (p.Position != null && p.Position.Length == 3)
            {
                var pos = new Vector3(p.Position[0], p.Position[1], p.Position[2]);
                if (isLocal)
                    go.transform.localPosition = pos;
                else
                    go.transform.position = pos;
            }

            if (p.Rotation != null && p.Rotation.Length == 3)
            {
                var rot = new Vector3(p.Rotation[0], p.Rotation[1], p.Rotation[2]);
                if (isLocal)
                    go.transform.localEulerAngles = rot;
                else
                    go.transform.eulerAngles = rot;
            }

            if (p.Scale != null && p.Scale.Length == 3)
            {
                // Scale is always local in Unity
                go.transform.localScale = new Vector3(p.Scale[0], p.Scale[1], p.Scale[2]);
            }

            return ToolResult<GameObjectSetTransformResult>.Ok(new GameObjectSetTransformResult
            {
                Name     = go.name,
                Position = GameObjectToolHelpers.ToFloatArray(go.transform.position),
                Rotation = GameObjectToolHelpers.ToFloatArray(go.transform.eulerAngles),
                Scale    = GameObjectToolHelpers.ToFloatArray(go.transform.localScale)
            });
        }
    }
}
