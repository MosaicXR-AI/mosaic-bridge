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
    public static class ProBuilderInfoTool
    {
        [MosaicTool("probuilder/info",
                    "Queries ProBuilder mesh info. If GameObjectName is null, returns all ProBuilder meshes in the scene.",
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
                    Meshes = new[] { BuildMeshInfo(pb) }
                });
            }

            // Return all ProBuilder meshes in the scene
            var allMeshes = Object.FindObjectsByType<ProBuilderMesh>(FindObjectsSortMode.None);
            if (allMeshes.Length == 0)
                return ToolResult<ProBuilderInfoResult>.Ok(new ProBuilderInfoResult
                {
                    Meshes = System.Array.Empty<ProBuilderMeshInfo>()
                });

            var meshInfos = allMeshes.Select(BuildMeshInfo).ToArray();
            return ToolResult<ProBuilderInfoResult>.Ok(new ProBuilderInfoResult
            {
                Meshes = meshInfos
            });
        }

        private static ProBuilderMeshInfo BuildMeshInfo(ProBuilderMesh pb)
        {
            var edges = new HashSet<Edge>();
            foreach (var face in pb.faces)
                foreach (var edge in face.edges)
                    edges.Add(edge);

            var renderer = pb.GetComponent<MeshRenderer>();
            var materialNames = renderer != null && renderer.sharedMaterials != null
                ? renderer.sharedMaterials
                    .Where(m => m != null)
                    .Select(m => m.name)
                    .ToArray()
                : System.Array.Empty<string>();

            return new ProBuilderMeshInfo
            {
                Name = pb.gameObject.name,
                InstanceId = pb.gameObject.GetInstanceID(),
                VertexCount = pb.vertexCount,
                FaceCount = pb.faceCount,
                EdgeCount = edges.Count,
                SharedVertexCount = pb.sharedVertices != null ? pb.sharedVertices.Count : 0,
                Materials = materialNames
            };
        }
    }

    public sealed class ProBuilderInfoParams
    {
        public string GameObjectName { get; set; }
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
    }
}
#endif
