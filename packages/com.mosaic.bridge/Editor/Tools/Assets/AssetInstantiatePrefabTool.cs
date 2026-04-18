using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Assets
{
    public static class AssetInstantiatePrefabTool
    {
        [MosaicTool("asset/instantiate_prefab",
                    "Instantiates a prefab asset into the current scene",
                    isReadOnly: false)]
        public static ToolResult<AssetInstantiatePrefabResult> Execute(AssetInstantiatePrefabParams p)
        {
            if (string.IsNullOrEmpty(p.PrefabPath))
                return ToolResult<AssetInstantiatePrefabResult>.Fail(
                    "PrefabPath is required", ErrorCodes.INVALID_PARAM);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.PrefabPath);
            if (prefab == null)
                return ToolResult<AssetInstantiatePrefabResult>.Fail(
                    $"Prefab not found at path '{p.PrefabPath}'", ErrorCodes.NOT_FOUND);

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return ToolResult<AssetInstantiatePrefabResult>.Fail(
                    $"Failed to instantiate prefab at '{p.PrefabPath}'", ErrorCodes.INTERNAL_ERROR);

            if (!string.IsNullOrEmpty(p.Name))
                instance.name = p.Name;

            var position = Vector3.zero;
            if (p.Position != null && p.Position.Length >= 3)
                position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            var rotation = Quaternion.identity;
            if (p.Rotation != null && p.Rotation.Length >= 3)
                rotation = Quaternion.Euler(p.Rotation[0], p.Rotation[1], p.Rotation[2]);

            instance.transform.position = position;
            instance.transform.rotation = rotation;

            Undo.RegisterCreatedObjectUndo(instance, "Mosaic: Instantiate Prefab");

            return ToolResult<AssetInstantiatePrefabResult>.Ok(new AssetInstantiatePrefabResult
            {
                Name       = instance.name,
                InstanceId = instance.GetInstanceID(),
                PrefabPath = p.PrefabPath,
                Position   = new float[] { position.x, position.y, position.z }
            });
        }
    }
}
