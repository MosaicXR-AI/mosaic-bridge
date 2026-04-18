using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsSetPhysicsMaterialTool
    {
        [MosaicTool("physics/set-physics-material",
                    "Creates and assigns a PhysicMaterial to a GameObject's collider",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsSetPhysicsMaterialResult> Execute(PhysicsSetPhysicsMaterialParams p)
        {
            var go = PhysicsToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<PhysicsSetPhysicsMaterialResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found",
                    ErrorCodes.NOT_FOUND);

            var collider = go.GetComponent<Collider>();
            if (collider == null)
                return ToolResult<PhysicsSetPhysicsMaterialResult>.Fail(
                    $"GameObject '{go.name}' has no Collider component",
                    ErrorCodes.INVALID_PARAM);

            Undo.RecordObject(collider, "Mosaic: Set PhysicMaterial");

            var mat = new PhysicsMaterial
            {
                dynamicFriction = p.DynamicFriction,
                staticFriction  = p.StaticFriction,
                bounciness      = p.Bounciness
            };

            bool savedAsAsset = false;
            string assetPath = null;

            if (!string.IsNullOrEmpty(p.AssetPath))
            {
                var absoluteDir = Path.GetDirectoryName(
                    Path.Combine(Application.dataPath, "..", p.AssetPath));
                if (!string.IsNullOrEmpty(absoluteDir))
                    Directory.CreateDirectory(absoluteDir);

                AssetDatabase.CreateAsset(mat, p.AssetPath);
                AssetDatabase.SaveAssets();
                savedAsAsset = true;
                assetPath = p.AssetPath;
            }

            collider.sharedMaterial = mat;

            return ToolResult<PhysicsSetPhysicsMaterialResult>.Ok(new PhysicsSetPhysicsMaterialResult
            {
                GameObjectName  = go.name,
                InstanceId      = go.GetInstanceID(),
                DynamicFriction = mat.dynamicFriction,
                StaticFriction  = mat.staticFriction,
                Bounciness      = mat.bounciness,
                AssetPath       = assetPath,
                SavedAsAsset    = savedAsAsset
            });
        }
    }
}
