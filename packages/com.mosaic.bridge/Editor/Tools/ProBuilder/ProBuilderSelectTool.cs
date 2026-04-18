#if MOSAIC_HAS_PROBUILDER
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ProBuilder
{
    public static class ProBuilderSelectTool
    {
        [MosaicTool("probuilder/select",
                    "Selects vertices, edges, or faces on a ProBuilder mesh",
                    isReadOnly: false, category: "probuilder")]
        public static ToolResult<ProBuilderSelectResult> Select(ProBuilderSelectParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<ProBuilderSelectResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<ProBuilderSelectResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var pb = go.GetComponent<ProBuilderMesh>();
            if (pb == null)
                return ToolResult<ProBuilderSelectResult>.Fail(
                    $"GameObject '{p.GameObjectName}' does not have a ProBuilderMesh component",
                    ErrorCodes.NOT_FOUND);

            if (string.IsNullOrEmpty(p.Mode))
                return ToolResult<ProBuilderSelectResult>.Fail(
                    "Mode is required. Valid: vertex, edge, face", ErrorCodes.INVALID_PARAM);

            if (p.Indices == null || p.Indices.Length == 0)
                return ToolResult<ProBuilderSelectResult>.Fail(
                    "Indices is required and must contain at least one index",
                    ErrorCodes.INVALID_PARAM);

            int selectedCount;
            var mode = p.Mode.ToLowerInvariant();

            switch (mode)
            {
                case "vertex":
                {
                    var positions = pb.positions;
                    foreach (var idx in p.Indices)
                    {
                        if (idx < 0 || idx >= positions.Count)
                            return ToolResult<ProBuilderSelectResult>.Fail(
                                $"Vertex index {idx} is out of range (0-{positions.Count - 1})",
                                ErrorCodes.OUT_OF_RANGE);
                    }
                    pb.SetSelectedVertices(p.Indices);
                    selectedCount = p.Indices.Length;
                    break;
                }
                case "edge":
                {
                    var allEdges = new List<Edge>();
                    foreach (var face in pb.faces)
                        allEdges.AddRange(face.edges);
                    var distinctEdges = allEdges.Distinct().ToList();

                    var selectedEdges = new List<Edge>();
                    foreach (var idx in p.Indices)
                    {
                        if (idx < 0 || idx >= distinctEdges.Count)
                            return ToolResult<ProBuilderSelectResult>.Fail(
                                $"Edge index {idx} is out of range (0-{distinctEdges.Count - 1})",
                                ErrorCodes.OUT_OF_RANGE);
                        selectedEdges.Add(distinctEdges[idx]);
                    }
                    pb.SetSelectedEdges(selectedEdges);
                    selectedCount = selectedEdges.Count;
                    break;
                }
                case "face":
                {
                    var allFaces = pb.faces;
                    var selectedFaces = new List<Face>();
                    foreach (var idx in p.Indices)
                    {
                        if (idx < 0 || idx >= allFaces.Count)
                            return ToolResult<ProBuilderSelectResult>.Fail(
                                $"Face index {idx} is out of range (0-{allFaces.Count - 1})",
                                ErrorCodes.OUT_OF_RANGE);
                        selectedFaces.Add(allFaces[idx]);
                    }
                    pb.SetSelectedFaces(selectedFaces);
                    selectedCount = selectedFaces.Count;
                    break;
                }
                default:
                    return ToolResult<ProBuilderSelectResult>.Fail(
                        $"Invalid Mode '{p.Mode}'. Valid: vertex, edge, face",
                        ErrorCodes.INVALID_PARAM);
            }

            return ToolResult<ProBuilderSelectResult>.Ok(new ProBuilderSelectResult
            {
                Mode = mode,
                SelectedCount = selectedCount
            });
        }
    }

    public sealed class ProBuilderSelectParams
    {
        [Required] public string GameObjectName { get; set; }
        [Required] public string Mode { get; set; }
        [Required] public int[] Indices { get; set; }
    }

    public sealed class ProBuilderSelectResult
    {
        public string Mode { get; set; }
        public int SelectedCount { get; set; }
    }
}
#endif
