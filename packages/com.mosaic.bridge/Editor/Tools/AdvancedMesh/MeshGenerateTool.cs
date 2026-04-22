using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public static class MeshGenerateTool
    {
        [MosaicTool("mesh/generate",
                    "Creates a mesh from raw vertex, triangle, UV, and normal data and saves as an asset. " +
                    "Vertices is a FLAT float array: [x0,y0,z0, x1,y1,z1, ...] (NOT nested arrays). " +
                    "Triangles is a flat int array of indices: [0,1,2, ...]. " +
                    "Example: Vertices=[0,0,0,1,0,0,0,1,0] Triangles=[0,1,2] creates one triangle.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<MeshGenerateResult> Execute(MeshGenerateParams p)
        {
            if (p.Vertices == null || p.Vertices.Length < 9)
                return ToolResult<MeshGenerateResult>.Fail(
                    "Vertices must contain at least 9 floats (3 vertices x 3 components)",
                    ErrorCodes.INVALID_PARAM);

            if (p.Vertices.Length % 3 != 0)
                return ToolResult<MeshGenerateResult>.Fail(
                    "Vertices array length must be divisible by 3 (x,y,z triplets)",
                    ErrorCodes.INVALID_PARAM);

            if (p.Triangles == null || p.Triangles.Length < 3)
                return ToolResult<MeshGenerateResult>.Fail(
                    "Triangles must contain at least 3 indices",
                    ErrorCodes.INVALID_PARAM);

            if (p.Triangles.Length % 3 != 0)
                return ToolResult<MeshGenerateResult>.Fail(
                    "Triangles array length must be divisible by 3",
                    ErrorCodes.INVALID_PARAM);

            int vertexCount = p.Vertices.Length / 3;

            // Validate triangle indices
            for (int i = 0; i < p.Triangles.Length; i++)
            {
                if (p.Triangles[i] < 0 || p.Triangles[i] >= vertexCount)
                    return ToolResult<MeshGenerateResult>.Fail(
                        $"Triangle index {p.Triangles[i]} at position {i} is out of range (0-{vertexCount - 1})",
                        ErrorCodes.OUT_OF_RANGE);
            }

            // Parse vertices
            var vertices = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                vertices[i] = new Vector3(p.Vertices[i * 3], p.Vertices[i * 3 + 1], p.Vertices[i * 3 + 2]);

            // Parse UVs
            bool hasUVs = false;
            Vector2[] uvs = null;
            if (p.UVs != null && p.UVs.Length > 0)
            {
                if (p.UVs.Length % 2 != 0)
                    return ToolResult<MeshGenerateResult>.Fail(
                        "UVs array length must be even (u,v pairs)", ErrorCodes.INVALID_PARAM);

                int uvCount = p.UVs.Length / 2;
                if (uvCount != vertexCount)
                    return ToolResult<MeshGenerateResult>.Fail(
                        $"UV count ({uvCount}) must match vertex count ({vertexCount})",
                        ErrorCodes.INVALID_PARAM);

                uvs = new Vector2[uvCount];
                for (int i = 0; i < uvCount; i++)
                    uvs[i] = new Vector2(p.UVs[i * 2], p.UVs[i * 2 + 1]);
                hasUVs = true;
            }

            // Parse normals
            bool hasNormals = false;
            Vector3[] normals = null;
            if (p.Normals != null && p.Normals.Length > 0)
            {
                if (p.Normals.Length % 3 != 0)
                    return ToolResult<MeshGenerateResult>.Fail(
                        "Normals array length must be divisible by 3 (x,y,z triplets)",
                        ErrorCodes.INVALID_PARAM);

                int normalCount = p.Normals.Length / 3;
                if (normalCount != vertexCount)
                    return ToolResult<MeshGenerateResult>.Fail(
                        $"Normal count ({normalCount}) must match vertex count ({vertexCount})",
                        ErrorCodes.INVALID_PARAM);

                normals = new Vector3[normalCount];
                for (int i = 0; i < normalCount; i++)
                    normals[i] = new Vector3(p.Normals[i * 3], p.Normals[i * 3 + 1], p.Normals[i * 3 + 2]);
                hasNormals = true;
            }

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(p.OutputPath) };
            if (vertexCount > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = vertices;
            mesh.triangles = p.Triangles;

            if (hasUVs) mesh.uv = uvs;
            if (hasNormals) mesh.normals = normals;
            else mesh.RecalculateNormals();

            mesh.RecalculateBounds();

            AssetDatabaseHelper.EnsureFolderForAsset(p.OutputPath);

            try
            {
                AssetDatabase.CreateAsset(mesh, p.OutputPath);
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                return ToolResult<MeshGenerateResult>.Fail(
                    $"Failed to save mesh asset at '{p.OutputPath}': {ex.Message}",
                    ErrorCodes.INTERNAL_ERROR);
            }

            int? goId = null;
            if (p.CreateGameObject)
            {
                var go = new GameObject(mesh.name);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                go.AddComponent<MeshRenderer>();
                Undo.RegisterCreatedObjectUndo(go, "Create Generated Mesh");
                goId = go.GetInstanceID();
            }

            return ToolResult<MeshGenerateResult>.Ok(new MeshGenerateResult
            {
                OutputPath = p.OutputPath,
                VertexCount = vertexCount,
                TriangleCount = p.Triangles.Length / 3,
                HasUVs = hasUVs,
                HasNormals = hasNormals,
                GameObjectInstanceId = goId
            });
        }
    }
}
