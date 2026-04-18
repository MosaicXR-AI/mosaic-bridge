#if MOSAIC_HAS_PROBUILDER
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ProBuilder
{
    public static class ProBuilderExtrudeTool
    {
        [MosaicTool("probuilder/extrude",
                    "Extrudes selected faces on a ProBuilder mesh by a given distance",
                    isReadOnly: false, category: "probuilder")]
        public static ToolResult<ProBuilderExtrudeResult> Extrude(ProBuilderExtrudeParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<ProBuilderExtrudeResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<ProBuilderExtrudeResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var pb = go.GetComponent<ProBuilderMesh>();
            if (pb == null)
                return ToolResult<ProBuilderExtrudeResult>.Fail(
                    $"GameObject '{p.GameObjectName}' does not have a ProBuilderMesh component",
                    ErrorCodes.NOT_FOUND);

            if (p.FaceIndices == null || p.FaceIndices.Length == 0)
                return ToolResult<ProBuilderExtrudeResult>.Fail(
                    "FaceIndices is required and must contain at least one index",
                    ErrorCodes.INVALID_PARAM);

            var allFaces = pb.faces;
            var selectedFaces = new List<Face>();
            foreach (var idx in p.FaceIndices)
            {
                if (idx < 0 || idx >= allFaces.Count)
                    return ToolResult<ProBuilderExtrudeResult>.Fail(
                        $"Face index {idx} is out of range (0-{allFaces.Count - 1})",
                        ErrorCodes.OUT_OF_RANGE);
                selectedFaces.Add(allFaces[idx]);
            }

            Undo.RecordObject(pb, "Mosaic: ProBuilder Extrude");

            pb.Extrude(selectedFaces, ExtrudeMethod.FaceNormal, p.Distance);
            pb.ToMesh();
            pb.Refresh();

            return ToolResult<ProBuilderExtrudeResult>.Ok(new ProBuilderExtrudeResult
            {
                VertexCount = pb.vertexCount,
                FaceCount = pb.faceCount
            });
        }
    }

    public sealed class ProBuilderExtrudeParams
    {
        [Required] public string GameObjectName { get; set; }
        [Required] public int[] FaceIndices { get; set; }
        public float Distance { get; set; } = 1.0f;
    }

    public sealed class ProBuilderExtrudeResult
    {
        public int VertexCount { get; set; }
        public int FaceCount { get; set; }
    }
}
#endif
