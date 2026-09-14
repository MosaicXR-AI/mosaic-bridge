#if MOSAIC_HAS_PROBUILDER
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.ProBuilder
{
    public static class ProBuilderInfoTool
    {
        [MosaicTool("probuilder/info",
                    "Queries ProBuilder mesh info. If GameObjectName is null, returns all ProBuilder meshes in the " +
                    "scene (summary counts only). With GameObjectName set, Detail='faces'|'edges'|'vertices'|'all' " +
                    "also returns per-element data — the prerequisite for naming a face/edge/vertex before " +
                    "probuilder/modify: face Center/Normal are world-space, so 'the top face' is the one with the " +
                    "highest Center.y or a Normal closest to [0,1,0]. Edges are returned as [A,B] local vertex-index " +
                    "pairs — pass the same pairs back as probuilder/modify's Edges param (ProBuilder has no " +
                    "persistent edge index of its own).",
                    isReadOnly: true, category: "probuilder")]
        public static ToolResult<ProBuilderInfoResult> Info(ProBuilderInfoParams p)
        {
            if (!string.IsNullOrEmpty(p.GameObjectName))
            {
                var go = GameObject.Find(p.GameObjectName);
                if (go == null)
                    return ToolResult<ProBuilderInfoResult>.Fail(
                        $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

                var pb = go.GetComponent<ProBuilderMesh>();
                if (pb == null)
                    return ToolResult<ProBuilderInfoResult>.Fail(
                        $"GameObject '{p.GameObjectName}' does not have a ProBuilderMesh component",
                        ErrorCodes.NOT_FOUND);

                return ToolResult<ProBuilderInfoResult>.Ok(new ProBuilderInfoResult
                {
                    Meshes = new[] { BuildMeshInfo(pb, p.Detail) }
                });
            }

            // Return all ProBuilder meshes in the scene — summary counts only, regardless of
            // Detail: per-element data for every mesh in a scene is unbounded (O4 §4.2's own
            // "keep trimmed; page big meshes" note). Query one GameObjectName at a time for detail.
            var allMeshes = UnityIds.FindAll<ProBuilderMesh>();
            var meshInfos = allMeshes.Select(m => BuildMeshInfo(m, null)).ToArray();
            return ToolResult<ProBuilderInfoResult>.Ok(new ProBuilderInfoResult
            {
                Meshes = meshInfos
            });
        }

        private static ProBuilderMeshInfo BuildMeshInfo(ProBuilderMesh pb, string detail)
        {
            var edgeSet = new HashSet<Edge>();
            foreach (var face in pb.faces)
                foreach (var edge in face.edges)
                    edgeSet.Add(edge);

            var renderer = pb.GetComponent<MeshRenderer>();
            var materialNames = renderer != null && renderer.sharedMaterials != null
                ? renderer.sharedMaterials
                    .Where(m => m != null)
                    .Select(m => m.name)
                    .ToArray()
                : System.Array.Empty<string>();

            var info = new ProBuilderMeshInfo
            {
                Name = pb.gameObject.name,
                InstanceId = UnityIds.Of(pb.gameObject),
                VertexCount = pb.vertexCount,
                FaceCount = pb.faceCount,
                EdgeCount = edgeSet.Count,
                SharedVertexCount = pb.sharedVertices != null ? pb.sharedVertices.Count : 0,
                Materials = materialNames
            };

            var d = (detail ?? "").ToLowerInvariant();
            bool wantFaces = d == "faces" || d == "all";
            bool wantEdges = d == "edges" || d == "all";
            bool wantVertices = d == "vertices" || d == "all";
            if (!wantFaces && !wantEdges && !wantVertices) return info;

            var worldPositions = VertexPositioning.VerticesInWorldSpace(pb);

            if (wantFaces)
            {
                info.Faces = new ProBuilderFaceInfo[pb.faces.Count];
                for (int i = 0; i < pb.faces.Count; i++)
                {
                    var face = pb.faces[i];
                    var distinct = face.distinctIndexes.ToArray();
                    var center = Vector3.zero;
                    foreach (var vi in distinct) center += worldPositions[vi];
                    if (distinct.Length > 0) center /= distinct.Length;
                    var normal = UnityEngine.ProBuilder.Math.Normal(pb, face);

                    info.Faces[i] = new ProBuilderFaceInfo
                    {
                        Index = i,
                        DistinctIndexes = distinct,
                        SubmeshIndex = face.submeshIndex,
                        SmoothingGroup = face.smoothingGroup,
                        Normal = new[] { normal.x, normal.y, normal.z },
                        Center = new[] { center.x, center.y, center.z },
                    };
                }
            }

            if (wantEdges)
            {
                info.Edges = edgeSet.Select(e => new ProBuilderEdgeInfo { A = e.a, B = e.b }).ToArray();
            }

            if (wantVertices)
            {
                info.Vertices = new ProBuilderVertexInfo[worldPositions.Length];
                for (int i = 0; i < worldPositions.Length; i++)
                {
                    var wp = worldPositions[i];
                    info.Vertices[i] = new ProBuilderVertexInfo { Index = i, Position = new[] { wp.x, wp.y, wp.z } };
                }
            }

            return info;
        }
    }

    public sealed class ProBuilderInfoParams
    {
        public string GameObjectName { get; set; }

        /// <summary>"faces", "edges", "vertices", or "all". Ignored when GameObjectName is null.</summary>
        public string Detail { get; set; }
    }

    public sealed class ProBuilderInfoResult
    {
        public ProBuilderMeshInfo[] Meshes { get; set; }
    }

    public sealed class ProBuilderMeshInfo
    {
        public string Name { get; set; }
        public int InstanceId { get; set; }
        public int VertexCount { get; set; }
        public int FaceCount { get; set; }
        public int EdgeCount { get; set; }
        public int SharedVertexCount { get; set; }
        public string[] Materials { get; set; }

        /// <summary>Set only when Detail requested faces.</summary>
        public ProBuilderFaceInfo[] Faces { get; set; }
        /// <summary>Set only when Detail requested edges.</summary>
        public ProBuilderEdgeInfo[] Edges { get; set; }
        /// <summary>Set only when Detail requested vertices.</summary>
        public ProBuilderVertexInfo[] Vertices { get; set; }
    }

    public sealed class ProBuilderFaceInfo
    {
        public int Index { get; set; }
        public int[] DistinctIndexes { get; set; }
        public int SubmeshIndex { get; set; }
        public int SmoothingGroup { get; set; }
        /// <summary>World-space face normal [x,y,z].</summary>
        public float[] Normal { get; set; }
        /// <summary>World-space center of the face's distinct vertices [x,y,z].</summary>
        public float[] Center { get; set; }
    }

    /// <summary>Local vertex-index pair — ProBuilder's own Edge(a,b) shape. Pass back as one of
    /// probuilder/modify's Edges entries; there is no separate persistent edge index to invent.</summary>
    public sealed class ProBuilderEdgeInfo
    {
        public int A { get; set; }
        public int B { get; set; }
    }

    public sealed class ProBuilderVertexInfo
    {
        public int Index { get; set; }
        /// <summary>World-space position [x,y,z].</summary>
        public float[] Position { get; set; }
    }
}
#endif
