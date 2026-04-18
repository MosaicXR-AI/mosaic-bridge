#if MOSAIC_HAS_URP
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.URP
{
    public static class UrpDecalTool
    {
        [MosaicTool("urp/decal",
                    "Create and configure a URP DecalProjector on a new GameObject",
                    isReadOnly: false,
                    category: "urp")]
        public static ToolResult<UrpDecalResult> Execute(UrpDecalParams p)
        {
            var go = new GameObject(p.Name);
            var decal = go.AddComponent<DecalProjector>();

            // Position
            if (p.Position != null && p.Position.Length >= 3)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            // Rotation
            if (p.Rotation != null && p.Rotation.Length >= 3)
                go.transform.eulerAngles = new Vector3(p.Rotation[0], p.Rotation[1], p.Rotation[2]);

            // Size
            if (p.Size != null && p.Size.Length >= 3)
                decal.size = new Vector3(p.Size[0], p.Size[1], p.Size[2]);

            // Material
            string materialName = null;
            if (!string.IsNullOrEmpty(p.MaterialPath))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p.MaterialPath);
                if (mat == null)
                {
                    Object.DestroyImmediate(go);
                    return ToolResult<UrpDecalResult>.Fail(
                        $"Material not found at '{p.MaterialPath}'.",
                        ErrorCodes.NOT_FOUND);
                }
                decal.material = mat;
                materialName = mat.name;
            }

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: URP Create Decal");

            var pos = go.transform.position;
            var rot = go.transform.eulerAngles;
            var size = decal.size;

            return ToolResult<UrpDecalResult>.Ok(new UrpDecalResult
            {
                InstanceId = go.GetInstanceID(),
                Name = go.name,
                HierarchyPath = UrpToolHelpers.GetHierarchyPath(go.transform),
                Size = new[] { size.x, size.y, size.z },
                Position = new[] { pos.x, pos.y, pos.z },
                Rotation = new[] { rot.x, rot.y, rot.z },
                MaterialName = materialName
            });
        }
    }
}
#endif
