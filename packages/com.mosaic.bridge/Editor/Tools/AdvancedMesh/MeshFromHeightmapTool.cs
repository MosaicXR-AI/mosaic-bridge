using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public static class MeshFromHeightmapTool
    {
        [MosaicTool("mesh/from-heightmap",
                    "Generates a terrain mesh from a heightmap texture with configurable resolution and scale",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<MeshFromHeightmapResult> Execute(MeshFromHeightmapParams p)
        {
            if (string.IsNullOrEmpty(p.HeightmapPath))
                return ToolResult<MeshFromHeightmapResult>.Fail(
                    "HeightmapPath is required", ErrorCodes.INVALID_PARAM);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(p.HeightmapPath);
            if (texture == null)
                return ToolResult<MeshFromHeightmapResult>.Fail(
                    $"Texture not found at '{p.HeightmapPath}'", ErrorCodes.NOT_FOUND);

            if (!texture.isReadable)
                return ToolResult<MeshFromHeightmapResult>.Fail(
                    "Heightmap texture is not readable. Enable Read/Write in import settings.",
                    ErrorCodes.INVALID_PARAM);

            int res = Mathf.Clamp(p.Resolution, 2, 512);
            float width = Mathf.Max(0.01f, p.Width);
            float depth = Mathf.Max(0.01f, p.Depth);
            float heightScale = p.HeightScale;

            int vertCount = res * res;
            int triCount = (res - 1) * (res - 1) * 2;

            // Check Unity mesh vertex limit
            if (vertCount > 65535)
            {
                // Clamp resolution to stay under limit
                res = (int)Mathf.Floor(Mathf.Sqrt(65535f));
                vertCount = res * res;
                triCount = (res - 1) * (res - 1) * 2;
            }

            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var triangles = new int[triCount * 3];

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);
                    float v = (float)z / (res - 1);

                    Color pixel = texture.GetPixelBilinear(u, v);
                    float h = pixel.grayscale * heightScale;

                    int idx = z * res + x;
                    vertices[idx] = new Vector3(u * width - width * 0.5f, h, v * depth - depth * 0.5f);
                    uvs[idx] = new Vector2(u, v);
                }
            }

            int ti = 0;
            for (int z = 0; z < res - 1; z++)
            {
                for (int x = 0; x < res - 1; x++)
                {
                    int bl = z * res + x;
                    int br = bl + 1;
                    int tl = bl + res;
                    int tr = tl + 1;

                    triangles[ti++] = bl;
                    triangles[ti++] = tl;
                    triangles[ti++] = br;

                    triangles[ti++] = br;
                    triangles[ti++] = tl;
                    triangles[ti++] = tr;
                }
            }

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(p.OutputPath) };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string absoluteDir = Path.GetDirectoryName(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", p.OutputPath)));
            if (!string.IsNullOrEmpty(absoluteDir))
                Directory.CreateDirectory(absoluteDir);

            AssetDatabase.CreateAsset(mesh, p.OutputPath);
            AssetDatabase.SaveAssets();

            int? goId = null;
            if (p.CreateGameObject)
            {
                var go = new GameObject(mesh.name);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                go.AddComponent<MeshRenderer>();
                Undo.RegisterCreatedObjectUndo(go, "Create Heightmap Mesh");
                goId = go.GetInstanceID();
            }

            return ToolResult<MeshFromHeightmapResult>.Ok(new MeshFromHeightmapResult
            {
                OutputPath = p.OutputPath,
                VertexCount = vertCount,
                TriangleCount = triCount,
                Width = width,
                Depth = depth,
                HeightScale = heightScale,
                GameObjectInstanceId = goId
            });
        }
    }
}
