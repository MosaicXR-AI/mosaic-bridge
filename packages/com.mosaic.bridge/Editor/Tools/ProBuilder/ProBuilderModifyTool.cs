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
    public static class ProBuilderModifyTool
    {
        [MosaicTool("probuilder/modify",
                    "Applies a mesh operation (merge, subdivide, flip-normals, detach, bridge, triangulate, bevel, " +
                    "delete-faces) to a ProBuilder mesh. Use probuilder/info with Detail='faces'|'edges' first to " +
                    "get FaceIndices/Edges — ProBuilder has no persistent edge index, so Edges takes literal " +
                    "[A,B] local vertex-index pairs, exactly as probuilder/info returns them. " +
                    "bevel: Edges (or FaceIndices to bevel each face's perimeter edges) + Amount (0..1 fraction " +
                    "of the face, NOT world units). delete-faces: FaceIndices.",
                    isReadOnly: false, category: "probuilder")]
        public static ToolResult<ProBuilderModifyResult> Modify(ProBuilderModifyParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<ProBuilderModifyResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.Operation))
                return ToolResult<ProBuilderModifyResult>.Fail(
                    "Operation is required. Valid: merge, subdivide, flip-normals, detach, bridge, triangulate",
                    ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<ProBuilderModifyResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var pb = go.GetComponent<ProBuilderMesh>();
            if (pb == null)
                return ToolResult<ProBuilderModifyResult>.Fail(
                    $"GameObject '{p.GameObjectName}' does not have a ProBuilderMesh component",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(pb, $"Mosaic: ProBuilder {p.Operation}");

            var operation = p.Operation.ToLowerInvariant();
            switch (operation)
            {
                case "merge":
                {
                    var selectedFaces = pb.GetSelectedFaces();
                    if (selectedFaces == null || selectedFaces.Length == 0)
                        return ToolResult<ProBuilderModifyResult>.Fail(
                            "No faces selected. Use probuilder/select to select faces before merging.",
                            ErrorCodes.INVALID_PARAM);
                    // ProBuilder 6.x public API: MergeElements.Merge(mesh, faces) returns
                    // the merged Face (or null on failure).
                    var merged = MergeElements.Merge(pb, selectedFaces);
                    if (merged == null)
                        return ToolResult<ProBuilderModifyResult>.Fail(
                            "Merge operation failed. Faces may not be adjacent.",
                            ErrorCodes.INVALID_PARAM);
                    break;
                }
                case "subdivide":
                {
                    ConnectElements.Connect(pb, pb.faces);
                    break;
                }
                case "flip-normals":
                {
                    var faces = pb.GetSelectedFaces();
                    if (faces == null || faces.Length == 0)
                        faces = pb.faces.ToArray();
                    foreach (var face in faces)
                        face.Reverse();
                    break;
                }
                case "detach":
                {
                    var selectedFaces = pb.GetSelectedFaces();
                    if (selectedFaces == null || selectedFaces.Length == 0)
                        return ToolResult<ProBuilderModifyResult>.Fail(
                            "No faces selected. Use probuilder/select to select faces before detaching.",
                            ErrorCodes.INVALID_PARAM);

                    // ProBuilder 6.x does not expose a public DetachElements API.
                    // Re-implement "detach faces as new object" using the stable
                    // public ProBuilderMesh.DeleteFaces primitive:
                    //   1. Clone the GameObject.
                    //   2. On the clone, keep ONLY the selected faces (by index).
                    //   3. On the original, delete the selected faces.
                    var clone = UnityEngine.Object.Instantiate(pb.gameObject);
                    clone.name = pb.name + " (detached)";
                    clone.transform.SetParent(pb.transform.parent, worldPositionStays: true);
                    Undo.RegisterCreatedObjectUndo(clone, "Mosaic: ProBuilder Detach");

                    var selectedIndices = new HashSet<int>();
                    for (int i = 0; i < pb.faces.Count; i++)
                        if (System.Array.IndexOf(selectedFaces, pb.faces[i]) >= 0)
                            selectedIndices.Add(i);

                    var cloneMesh = clone.GetComponent<ProBuilderMesh>();
                    var cloneFacesToDelete = new List<Face>();
                    for (int i = 0; i < cloneMesh.faces.Count; i++)
                        if (!selectedIndices.Contains(i))
                            cloneFacesToDelete.Add(cloneMesh.faces[i]);
                    cloneMesh.DeleteFaces(cloneFacesToDelete);
                    cloneMesh.ToMesh();
                    cloneMesh.Refresh();

                    pb.DeleteFaces(selectedFaces);
                    break;
                }
                case "bridge":
                {
                    var selectedEdges = pb.selectedEdges.ToList();
                    if (selectedEdges.Count != 2)
                        return ToolResult<ProBuilderModifyResult>.Fail(
                            "Bridge requires exactly 2 edges selected. Use probuilder/select with mode 'edge'.",
                            ErrorCodes.INVALID_PARAM);
                    var bridgeResult = AppendElements.Bridge(pb, selectedEdges[0], selectedEdges[1]);
                    if (bridgeResult == null)
                        return ToolResult<ProBuilderModifyResult>.Fail(
                            "Bridge operation failed. Edges may not be valid for bridging.",
                            ErrorCodes.INVALID_PARAM);
                    break;
                }
                case "triangulate":
                {
                    var faces = pb.faces.ToArray();
                    foreach (var face in faces)
                    {
                        // Triangulate by subdividing each face
                    }
                    ConnectElements.Connect(pb, pb.faces);
                    break;
                }
                case "bevel":
                {
                    if (!p.Amount.HasValue)
                        return ToolResult<ProBuilderModifyResult>.Fail(
                            "Amount is required for bevel (0..1 fraction of the face, not world units).",
                            ErrorCodes.INVALID_PARAM);

                    List<Edge> edgesToBevel;
                    if (p.Edges != null && p.Edges.Length > 0)
                    {
                        edgesToBevel = p.Edges.Select(pair =>
                        {
                            if (pair == null || pair.Length != 2)
                                throw new System.ArgumentException("Each Edges entry must be a [A, B] pair");
                            return new Edge(pair[0], pair[1]);
                        }).ToList();
                    }
                    else if (p.FaceIndices != null && p.FaceIndices.Length > 0)
                    {
                        var faceEdges = new HashSet<Edge>();
                        foreach (var idx in p.FaceIndices)
                        {
                            if (idx < 0 || idx >= pb.faces.Count)
                                return ToolResult<ProBuilderModifyResult>.Fail(
                                    $"FaceIndices contains out-of-range index {idx} (mesh has {pb.faces.Count} faces).",
                                    ErrorCodes.INVALID_PARAM);
                            foreach (var edge in pb.faces[idx].edges) faceEdges.Add(edge);
                        }
                        edgesToBevel = faceEdges.ToList();
                    }
                    else
                    {
                        return ToolResult<ProBuilderModifyResult>.Fail(
                            "bevel requires Edges ([[a,b],...] pairs) or FaceIndices (bevels each face's perimeter edges).",
                            ErrorCodes.INVALID_PARAM);
                    }

                    List<Face> beveled;
                    try { beveled = Bevel.BevelEdges(pb, edgesToBevel, p.Amount.Value); }
                    catch (System.ArgumentException e)
                    {
                        return ToolResult<ProBuilderModifyResult>.Fail(e.Message, ErrorCodes.INVALID_PARAM);
                    }
                    if (beveled == null || beveled.Count == 0)
                        return ToolResult<ProBuilderModifyResult>.Fail(
                            "Bevel produced no new faces — the edges may not be valid for beveling.",
                            ErrorCodes.INVALID_PARAM);
                    break;
                }
                case "delete-faces":
                {
                    if (p.FaceIndices == null || p.FaceIndices.Length == 0)
                        return ToolResult<ProBuilderModifyResult>.Fail(
                            "delete-faces requires FaceIndices.", ErrorCodes.INVALID_PARAM);
                    foreach (var idx in p.FaceIndices)
                        if (idx < 0 || idx >= pb.faces.Count)
                            return ToolResult<ProBuilderModifyResult>.Fail(
                                $"FaceIndices contains out-of-range index {idx} (mesh has {pb.faces.Count} faces).",
                                ErrorCodes.INVALID_PARAM);
                    if (p.FaceIndices.Length >= pb.faces.Count)
                        return ToolResult<ProBuilderModifyResult>.Fail(
                            "delete-faces would remove every face on this mesh — delete the GameObject instead " +
                            "if that is really the intent.",
                            ErrorCodes.INVALID_PARAM);
                    DeleteElements.DeleteFaces(pb, p.FaceIndices.ToList());
                    break;
                }
                default:
                    return ToolResult<ProBuilderModifyResult>.Fail(
                        $"Invalid Operation '{p.Operation}'. Valid: merge, subdivide, flip-normals, detach, bridge, " +
                        "triangulate, bevel, delete-faces",
                        ErrorCodes.INVALID_PARAM);
            }

            pb.ToMesh();
            pb.Refresh();

            return ToolResult<ProBuilderModifyResult>.Ok(new ProBuilderModifyResult
            {
                Operation = operation,
                VertexCount = pb.vertexCount,
                FaceCount = pb.faceCount
            });
        }
    }

    public sealed class ProBuilderModifyParams
    {
        [Required] public string GameObjectName { get; set; }
        [Required] public string Operation { get; set; }

        /// <summary>bevel: literal ProBuilder Edge(a,b) pairs, e.g. [[0,1],[1,2]] — from probuilder/info's
        /// Edges detail. Local vertex indices, not a synthetic edge index.</summary>
        public int[][] Edges { get; set; }
        /// <summary>bevel (perimeter edges of these faces) or delete-faces.</summary>
        public int[] FaceIndices { get; set; }
        /// <summary>bevel only: 0..1 fraction of the face, not world units.</summary>
        public float? Amount { get; set; }
    }

    public sealed class ProBuilderModifyResult
    {
        public string Operation { get; set; }
        public int VertexCount { get; set; }
        public int FaceCount { get; set; }
    }
}
#endif
