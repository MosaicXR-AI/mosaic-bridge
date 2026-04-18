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
    public static class MeshDualContourTool
    {
        private const int MaxResolution = 64;
        private const int MinResolution = 4;

        private static readonly string[] ValidSdfFunctions = { "sphere", "box", "torus", "noise" };

        [MosaicTool("mesh/dual-contour",
                    "Extracts a mesh from a built-in SDF using Dual Contouring (sphere/box/torus/noise)",
                    isReadOnly: false, category: "mesh", Context = ToolContext.Both)]
        public static ToolResult<MeshDualContourResult> Execute(MeshDualContourParams p)
        {
            if (p == null)
                return ToolResult<MeshDualContourResult>.Fail("Params cannot be null", ErrorCodes.INVALID_PARAM);

            string sdfFn = (p.SdfFunction ?? "sphere").Trim().ToLowerInvariant();
            if (Array.IndexOf(ValidSdfFunctions, sdfFn) < 0)
                return ToolResult<MeshDualContourResult>.Fail(
                    $"Invalid SdfFunction '{p.SdfFunction}'. Valid: {string.Join(", ", ValidSdfFunctions)}",
                    ErrorCodes.INVALID_PARAM);

            int res = Mathf.Clamp(p.Resolution, MinResolution, MaxResolution);

            Vector3 boundsMin = p.BoundsMin != null && p.BoundsMin.Length == 3
                ? new Vector3(p.BoundsMin[0], p.BoundsMin[1], p.BoundsMin[2])
                : new Vector3(-10f, -10f, -10f);
            Vector3 boundsMax = p.BoundsMax != null && p.BoundsMax.Length == 3
                ? new Vector3(p.BoundsMax[0], p.BoundsMax[1], p.BoundsMax[2])
                : new Vector3(10f, 10f, 10f);

            if (boundsMax.x <= boundsMin.x || boundsMax.y <= boundsMin.y || boundsMax.z <= boundsMin.z)
                return ToolResult<MeshDualContourResult>.Fail(
                    "BoundsMax must be greater than BoundsMin on all axes", ErrorCodes.INVALID_PARAM);

            string savePath = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/Mesh/" : p.SavePath;
            if (!savePath.StartsWith("Assets/"))
                return ToolResult<MeshDualContourResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);
            if (!savePath.EndsWith("/")) savePath += "/";

            // Sample SDF on grid
            int gridSize = res + 1;
            float[,,] sdf = new float[gridSize, gridSize, gridSize];
            Vector3 cellSize = new Vector3(
                (boundsMax.x - boundsMin.x) / res,
                (boundsMax.y - boundsMin.y) / res,
                (boundsMax.z - boundsMin.z) / res);

            float sdfRadius = p.SdfRadius;
            Vector3 sdfBoxSize = p.SdfSize != null && p.SdfSize.Length == 3
                ? new Vector3(p.SdfSize[0], p.SdfSize[1], p.SdfSize[2])
                : new Vector3(5f, 5f, 5f);

            for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 pos = boundsMin + new Vector3(x * cellSize.x, y * cellSize.y, z * cellSize.z);
                sdf[x, y, z] = EvaluateSdf(sdfFn, pos, sdfRadius, sdfBoxSize) - p.IsoValue;
            }

            // For each cell containing sign change, compute a representative vertex (cell center for MVP)
            int[,,] cellVertexIndex = new int[res, res, res];
            for (int x = 0; x < res; x++)
            for (int y = 0; y < res; y++)
            for (int z = 0; z < res; z++)
                cellVertexIndex[x, y, z] = -1;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int x = 0; x < res; x++)
            for (int y = 0; y < res; y++)
            for (int z = 0; z < res; z++)
            {
                if (HasSignChange(sdf, x, y, z))
                {
                    Vector3 cellCenter = boundsMin + new Vector3(
                        (x + 0.5f) * cellSize.x,
                        (y + 0.5f) * cellSize.y,
                        (z + 0.5f) * cellSize.z);
                    cellVertexIndex[x, y, z] = vertices.Count;
                    vertices.Add(cellCenter);
                }
            }

            // For each sign-changing edge shared by 4 cells, generate a quad (2 triangles)
            // Edge along X direction (between cells offset by Y±1, Z±1)
            for (int x = 0; x < res; x++)
            for (int y = 1; y < res; y++)
            for (int z = 1; z < res; z++)
            {
                float v1 = sdf[x, y, z];
                float v2 = sdf[x + 1, y, z];
                if ((v1 < 0) != (v2 < 0))
                {
                    // 4 cells share this edge: (x, y-1, z-1), (x, y, z-1), (x, y-1, z), (x, y, z)
                    int i0 = cellVertexIndex[x, y - 1, z - 1];
                    int i1 = cellVertexIndex[x, y, z - 1];
                    int i2 = cellVertexIndex[x, y, z];
                    int i3 = cellVertexIndex[x, y - 1, z];
                    if (i0 >= 0 && i1 >= 0 && i2 >= 0 && i3 >= 0)
                    {
                        bool flip = v1 > 0;
                        if (flip)
                        { triangles.Add(i0); triangles.Add(i2); triangles.Add(i1); triangles.Add(i0); triangles.Add(i3); triangles.Add(i2); }
                        else
                        { triangles.Add(i0); triangles.Add(i1); triangles.Add(i2); triangles.Add(i0); triangles.Add(i2); triangles.Add(i3); }
                    }
                }
            }

            // Edge along Y direction (between cells offset by X±1, Z±1)
            for (int x = 1; x < res; x++)
            for (int y = 0; y < res; y++)
            for (int z = 1; z < res; z++)
            {
                float v1 = sdf[x, y, z];
                float v2 = sdf[x, y + 1, z];
                if ((v1 < 0) != (v2 < 0))
                {
                    int i0 = cellVertexIndex[x - 1, y, z - 1];
                    int i1 = cellVertexIndex[x, y, z - 1];
                    int i2 = cellVertexIndex[x, y, z];
                    int i3 = cellVertexIndex[x - 1, y, z];
                    if (i0 >= 0 && i1 >= 0 && i2 >= 0 && i3 >= 0)
                    {
                        bool flip = v1 > 0;
                        if (flip)
                        { triangles.Add(i0); triangles.Add(i1); triangles.Add(i2); triangles.Add(i0); triangles.Add(i2); triangles.Add(i3); }
                        else
                        { triangles.Add(i0); triangles.Add(i2); triangles.Add(i1); triangles.Add(i0); triangles.Add(i3); triangles.Add(i2); }
                    }
                }
            }

            // Edge along Z direction (between cells offset by X±1, Y±1)
            for (int x = 1; x < res; x++)
            for (int y = 1; y < res; y++)
            for (int z = 0; z < res; z++)
            {
                float v1 = sdf[x, y, z];
                float v2 = sdf[x, y, z + 1];
                if ((v1 < 0) != (v2 < 0))
                {
                    int i0 = cellVertexIndex[x - 1, y - 1, z];
                    int i1 = cellVertexIndex[x, y - 1, z];
                    int i2 = cellVertexIndex[x, y, z];
                    int i3 = cellVertexIndex[x - 1, y, z];
                    if (i0 >= 0 && i1 >= 0 && i2 >= 0 && i3 >= 0)
                    {
                        bool flip = v1 > 0;
                        if (flip)
                        { triangles.Add(i0); triangles.Add(i2); triangles.Add(i1); triangles.Add(i0); triangles.Add(i3); triangles.Add(i2); }
                        else
                        { triangles.Add(i0); triangles.Add(i1); triangles.Add(i2); triangles.Add(i0); triangles.Add(i2); triangles.Add(i3); }
                    }
                }
            }

            if (vertices.Count == 0 || triangles.Count == 0)
                return ToolResult<MeshDualContourResult>.Fail(
                    "No surface extracted (SDF may not cross zero in the bounds)",
                    ErrorCodes.INTERNAL_ERROR);

            // Build mesh
            var mesh = new Mesh();
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Save asset
            Directory.CreateDirectory(Path.Combine(Application.dataPath.Replace("/Assets", ""), savePath));
            AssetDatabase.Refresh();
            string outputName = string.IsNullOrEmpty(p.OutputName) ? $"DualContour_{sdfFn}" : p.OutputName;
            string assetPath = savePath + outputName + ".asset";

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();

            return ToolResult<MeshDualContourResult>.Ok(new MeshDualContourResult
            {
                MeshPath = assetPath,
                VertexCount = vertices.Count,
                TriangleCount = triangles.Count / 3,
                Resolution = res,
                SdfFunction = sdfFn
            });
        }

        private static bool HasSignChange(float[,,] sdf, int x, int y, int z)
        {
            float baseSign = Mathf.Sign(sdf[x, y, z]);
            for (int dx = 0; dx <= 1; dx++)
            for (int dy = 0; dy <= 1; dy++)
            for (int dz = 0; dz <= 1; dz++)
            {
                if (Mathf.Sign(sdf[x + dx, y + dy, z + dz]) != baseSign) return true;
            }
            return false;
        }

        private static float EvaluateSdf(string fn, Vector3 p, float radius, Vector3 boxSize)
        {
            switch (fn)
            {
                case "sphere":
                    return p.magnitude - radius;
                case "box":
                {
                    Vector3 d = new Vector3(Mathf.Abs(p.x) - boxSize.x, Mathf.Abs(p.y) - boxSize.y, Mathf.Abs(p.z) - boxSize.z);
                    Vector3 dMax = new Vector3(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0), Mathf.Max(d.z, 0));
                    return dMax.magnitude + Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0);
                }
                case "torus":
                {
                    float r1 = radius;
                    float r2 = radius / 3f;
                    float qx = new Vector2(p.x, p.z).magnitude - r1;
                    return new Vector2(qx, p.y).magnitude - r2;
                }
                case "noise":
                {
                    float noise = (Mathf.PerlinNoise(p.x * 0.1f, p.y * 0.1f) - 0.5f) * radius * 0.5f;
                    return p.magnitude - radius + noise;
                }
                default:
                    return p.magnitude - radius;
            }
        }
    }
}
