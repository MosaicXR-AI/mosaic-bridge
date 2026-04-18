using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public static class MeshVoxelToMeshTool
    {
        // Edge-to-vertex index pairs for the 12 edges of a cube
        static readonly int[,] EdgeVertices = new int[12, 2]
        {
            {0,1}, {1,2}, {2,3}, {3,0},
            {4,5}, {5,6}, {6,7}, {7,4},
            {0,4}, {1,5}, {2,6}, {3,7}
        };

        // Corner offsets in the cube (x,y,z)
        static readonly Vector3Int[] CornerOffsets = new Vector3Int[8]
        {
            new Vector3Int(0,0,0), new Vector3Int(1,0,0),
            new Vector3Int(1,1,0), new Vector3Int(0,1,0),
            new Vector3Int(0,0,1), new Vector3Int(1,0,1),
            new Vector3Int(1,1,1), new Vector3Int(0,1,1)
        };

        [MosaicTool("mesh/voxel-to-mesh",
                    "Extracts a mesh from voxel data using the marching cubes algorithm",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<MeshVoxelToMeshResult> Execute(MeshVoxelToMeshParams p)
        {
            int gx = Mathf.Clamp(p.GridSizeX, 2, 64);
            int gy = Mathf.Clamp(p.GridSizeY, 2, 64);
            int gz = Mathf.Clamp(p.GridSizeZ, 2, 64);
            float isoLevel = p.IsoLevel;

            // Build voxel grid
            float[,,] grid = new float[gx, gy, gz];

            if (p.VoxelData != null && p.VoxelData.Length > 0)
            {
                int expected = gx * gy * gz;
                if (p.VoxelData.Length != expected)
                    return ToolResult<MeshVoxelToMeshResult>.Fail(
                        $"VoxelData length ({p.VoxelData.Length}) must equal GridSizeX*Y*Z ({expected})",
                        ErrorCodes.INVALID_PARAM);

                int idx = 0;
                for (int z = 0; z < gz; z++)
                    for (int y = 0; y < gy; y++)
                        for (int x = 0; x < gx; x++)
                            grid[x, y, z] = p.VoxelData[idx++];
            }
            else
            {
                // Generate sphere SDF: distance from center
                Vector3 center = new Vector3(gx * 0.5f, gy * 0.5f, gz * 0.5f);
                float radius = Mathf.Min(gx, Mathf.Min(gy, gz)) * 0.4f;

                for (int z = 0; z < gz; z++)
                    for (int y = 0; y < gy; y++)
                        for (int x = 0; x < gx; x++)
                        {
                            float dist = Vector3.Distance(
                                new Vector3(x, y, z), center);
                            grid[x, y, z] = 1f - (dist / radius);
                        }
            }

            // Marching cubes
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int z = 0; z < gz - 1; z++)
            {
                for (int y = 0; y < gy - 1; y++)
                {
                    for (int x = 0; x < gx - 1; x++)
                    {
                        MarchCube(x, y, z, grid, isoLevel, vertices, triangles);
                    }
                }
            }

            if (vertices.Count == 0)
                return ToolResult<MeshVoxelToMeshResult>.Fail(
                    "Marching cubes produced no geometry. Try adjusting IsoLevel or VoxelData.",
                    ErrorCodes.INVALID_PARAM);

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(p.OutputPath) };
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
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
                Undo.RegisterCreatedObjectUndo(go, "Create Voxel Mesh");
                goId = go.GetInstanceID();
            }

            return ToolResult<MeshVoxelToMeshResult>.Ok(new MeshVoxelToMeshResult
            {
                OutputPath = p.OutputPath,
                VertexCount = vertices.Count,
                TriangleCount = triangles.Count / 3,
                GridSize = $"{gx}x{gy}x{gz}",
                IsoLevel = isoLevel,
                GameObjectInstanceId = goId
            });
        }

        static void MarchCube(int x, int y, int z, float[,,] grid, float isoLevel,
            List<Vector3> vertices, List<int> triangles)
        {
            // Determine cube index from 8 corner values
            int cubeIndex = 0;
            float[] cornerValues = new float[8];

            for (int i = 0; i < 8; i++)
            {
                var off = CornerOffsets[i];
                cornerValues[i] = grid[x + off.x, y + off.y, z + off.z];
                if (cornerValues[i] >= isoLevel)
                    cubeIndex |= (1 << i);
            }

            int edgeMask = MarchingCubesTables.EdgeTable[cubeIndex];
            if (edgeMask == 0) return;

            // Interpolate vertices along edges
            Vector3[] edgeVertices = new Vector3[12];

            for (int i = 0; i < 12; i++)
            {
                if ((edgeMask & (1 << i)) == 0) continue;

                int v0 = EdgeVertices[i, 0];
                int v1 = EdgeVertices[i, 1];

                Vector3 p0 = new Vector3(
                    x + CornerOffsets[v0].x,
                    y + CornerOffsets[v0].y,
                    z + CornerOffsets[v0].z);
                Vector3 p1 = new Vector3(
                    x + CornerOffsets[v1].x,
                    y + CornerOffsets[v1].y,
                    z + CornerOffsets[v1].z);

                float val0 = cornerValues[v0];
                float val1 = cornerValues[v1];

                float t = Mathf.Abs(val1 - val0) > 0.00001f
                    ? (isoLevel - val0) / (val1 - val0)
                    : 0.5f;

                edgeVertices[i] = Vector3.Lerp(p0, p1, t);
            }

            // Generate triangles from the triangle table
            for (int i = 0; MarchingCubesTables.TriTable[cubeIndex, i] != -1; i += 3)
            {
                int baseIdx = vertices.Count;

                vertices.Add(edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i]]);
                vertices.Add(edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i + 1]]);
                vertices.Add(edgeVertices[MarchingCubesTables.TriTable[cubeIndex, i + 2]]);

                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);
            }
        }
    }
}
