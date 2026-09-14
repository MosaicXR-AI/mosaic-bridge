#if MOSAIC_HAS_PROBUILDER
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Scenes;

namespace Mosaic.Bridge.Tools.ProBuilder
{
    public static class ProBuilderSetMaterialTool
    {
        [MosaicTool("probuilder/set_material",
                    "Assigns a material to specific faces on a ProBuilder mesh",
                    isReadOnly: false, category: "probuilder")]
        public static ToolResult<ProBuilderSetMaterialResult> SetMaterial(ProBuilderSetMaterialParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<ProBuilderSetMaterialResult>.Fail(
                    "Either InstanceId or GameObjectName is required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.MaterialPath))
                return ToolResult<ProBuilderSetMaterialResult>.Fail(
                    "MaterialPath is required", ErrorCodes.INVALID_PARAM);

            if (p.FaceIndices == null || p.FaceIndices.Length == 0)
                return ToolResult<ProBuilderSetMaterialResult>.Fail(
                    "FaceIndices is required and must contain at least one index",
                    ErrorCodes.INVALID_PARAM);

            if (!GameObjectResolver.TryResolve(p.InstanceId, p.GameObjectName, out var go, out var resolveError))
                return ToolResult<ProBuilderSetMaterialResult>.Fail(resolveError, ErrorCodes.NOT_FOUND);

            var pb = go.GetComponent<ProBuilderMesh>();
            if (pb == null)
                return ToolResult<ProBuilderSetMaterialResult>.Fail(
                    $"GameObject '{p.GameObjectName}' does not have a ProBuilderMesh component",
                    ErrorCodes.NOT_FOUND);

            var material = AssetDatabase.LoadAssetAtPath<Material>(p.MaterialPath);
            if (material == null)
                return ToolResult<ProBuilderSetMaterialResult>.Fail(
                    $"Material not found at path '{p.MaterialPath}'", ErrorCodes.NOT_FOUND);

            var allFaces = pb.faces;
            var targetFaces = new List<Face>();
            foreach (var idx in p.FaceIndices)
            {
                if (idx < 0 || idx >= allFaces.Count)
                    return ToolResult<ProBuilderSetMaterialResult>.Fail(
                        $"Face index {idx} is out of range (0-{allFaces.Count - 1})",
                        ErrorCodes.OUT_OF_RANGE);
                targetFaces.Add(allFaces[idx]);
            }

            Undo.RecordObject(pb, "Mosaic: ProBuilder Set Material");

            // Find or add the material to the renderer's shared materials
            var renderer = pb.GetComponent<MeshRenderer>();
            var sharedMats = new List<Material>(renderer.sharedMaterials);
            int submeshIndex = sharedMats.IndexOf(material);
            if (submeshIndex < 0)
            {
                submeshIndex = sharedMats.Count;
                sharedMats.Add(material);
                renderer.sharedMaterials = sharedMats.ToArray();
            }

            foreach (var face in targetFaces)
                face.submeshIndex = submeshIndex;

            pb.ToMesh();
            pb.Refresh();

            return ToolResult<ProBuilderSetMaterialResult>.Ok(new ProBuilderSetMaterialResult
            {
                AffectedFaceCount = targetFaces.Count,
                MaterialName = material.name
            });
        }
    }

    public sealed class ProBuilderSetMaterialParams
    {
        public string GameObjectName { get; set; }
        public int? InstanceId { get; set; }
        [Required] public int[] FaceIndices { get; set; }
        [Required] public string MaterialPath { get; set; }
    }

    public sealed class ProBuilderSetMaterialResult
    {
        public int AffectedFaceCount { get; set; }
        public string MaterialName { get; set; }
    }
}
#endif
