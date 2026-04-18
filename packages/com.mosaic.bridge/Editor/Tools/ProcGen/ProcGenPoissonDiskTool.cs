using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public static class ProcGenPoissonDiskTool
    {
        private const int DefaultK = 30; // candidates per active point

        [MosaicTool("procgen/poisson-disk",
                    "Generates Poisson disk sample points using Bridson's algorithm with optional prefab instantiation and terrain/mesh projection",
                    isReadOnly: false, category: "procgen")]
        public static ToolResult<ProcGenPoissonDiskResult> Execute(ProcGenPoissonDiskParams p)
        {
            // --- Defaults & validation ---
            int dims = p.Dimensions ?? 2;
            if (dims != 2 && dims != 3)
                return ToolResult<ProcGenPoissonDiskResult>.Fail(
                    "Dimensions must be 2 or 3", ErrorCodes.INVALID_PARAM);

            if (p.MinDistance <= 0)
                return ToolResult<ProcGenPoissonDiskResult>.Fail(
                    "MinDistance must be greater than 0", ErrorCodes.INVALID_PARAM);

            if (p.BoundsMax == null || p.BoundsMax.Length < 3)
                return ToolResult<ProcGenPoissonDiskResult>.Fail(
                    "BoundsMax must be a float[3]", ErrorCodes.INVALID_PARAM);

            float[] bMin = p.BoundsMin ?? new float[] { 0f, 0f, 0f };
            float[] bMax = p.BoundsMax;

            if (bMin.Length < 3)
                return ToolResult<ProcGenPoissonDiskResult>.Fail(
                    "BoundsMin must be a float[3]", ErrorCodes.INVALID_PARAM);

            if (bMin[0] >= bMax[0] || bMin[2] >= bMax[2])
                return ToolResult<ProcGenPoissonDiskResult>.Fail(
                    "BoundsMax must be greater than BoundsMin on all used axes", ErrorCodes.INVALID_PARAM);

            if (dims == 3 && bMin[1] >= bMax[1])
                return ToolResult<ProcGenPoissonDiskResult>.Fail(
                    "BoundsMax.y must be greater than BoundsMin.y for 3D sampling", ErrorCodes.INVALID_PARAM);

            string surfaceMode = (p.SurfaceMode ?? "flat").ToLowerInvariant();
            if (surfaceMode != "flat" && surfaceMode != "terrain" && surfaceMode != "mesh")
                return ToolResult<ProcGenPoissonDiskResult>.Fail(
                    "SurfaceMode must be 'flat', 'terrain', or 'mesh'", ErrorCodes.INVALID_PARAM);

            // Validate prefab if specified
            GameObject prefab = null;
            if (!string.IsNullOrEmpty(p.PrefabPath))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.PrefabPath);
                if (prefab == null)
                    return ToolResult<ProcGenPoissonDiskResult>.Fail(
                        $"Prefab not found at path: {p.PrefabPath}", ErrorCodes.NOT_FOUND);
            }

            // Validate surface object if terrain/mesh mode
            Terrain terrain = null;
            MeshCollider meshCollider = null;
            if (surfaceMode == "terrain")
            {
                if (!string.IsNullOrEmpty(p.SurfaceObject))
                {
                    var go = GameObject.Find(p.SurfaceObject);
                    if (go != null) terrain = go.GetComponent<Terrain>();
                }
                if (terrain == null) terrain = Terrain.activeTerrain;
                if (terrain == null)
                    return ToolResult<ProcGenPoissonDiskResult>.Fail(
                        "No terrain found for SurfaceMode 'terrain'", ErrorCodes.NOT_FOUND);
            }
            else if (surfaceMode == "mesh")
            {
                if (string.IsNullOrEmpty(p.SurfaceObject))
                    return ToolResult<ProcGenPoissonDiskResult>.Fail(
                        "SurfaceObject is required for SurfaceMode 'mesh'", ErrorCodes.INVALID_PARAM);
                var go = GameObject.Find(p.SurfaceObject);
                if (go == null)
                    return ToolResult<ProcGenPoissonDiskResult>.Fail(
                        $"GameObject '{p.SurfaceObject}' not found for mesh projection", ErrorCodes.NOT_FOUND);
                meshCollider = go.GetComponent<MeshCollider>();
                if (meshCollider == null)
                    return ToolResult<ProcGenPoissonDiskResult>.Fail(
                        $"GameObject '{p.SurfaceObject}' has no MeshCollider for mesh projection", ErrorCodes.NOT_FOUND);
            }

            // Parent object
            Transform parent = null;
            if (!string.IsNullOrEmpty(p.ParentObject))
            {
                var parentGo = GameObject.Find(p.ParentObject);
                if (parentGo == null)
                {
                    parentGo = new GameObject(p.ParentObject);
                    Undo.RegisterCreatedObjectUndo(parentGo, "Poisson Disk - Create Parent");
                }
                parent = parentGo.transform;
            }

            // --- Bridson's algorithm ---
            var rng = p.Seed.HasValue ? new System.Random(p.Seed.Value) : new System.Random();
            float minDist = p.MinDistance;
            float cellSize = minDist / Mathf.Sqrt(dims);

            List<Vector3> points;
            if (dims == 2)
                points = SampleBridson2D(bMin, bMax, minDist, cellSize, rng, p.MaxSamples);
            else
                points = SampleBridson3D(bMin, bMax, minDist, cellSize, rng, p.MaxSamples);

            // --- Surface projection ---
            if (surfaceMode == "terrain" && terrain != null)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var pt = points[i];
                    pt.y = terrain.SampleHeight(pt);
                    points[i] = pt;
                }
            }
            else if (surfaceMode == "mesh" && meshCollider != null)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var pt = points[i];
                    var ray = new Ray(new Vector3(pt.x, bMax[1] + 100f, pt.z), Vector3.down);
                    if (meshCollider.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
                        pt.y = hit.point.y;
                    points[i] = pt;
                }
            }

            // --- Prefab instantiation ---
            var goNames = new List<string>();
            if (prefab != null)
            {
                Undo.SetCurrentGroupName("Poisson Disk - Instantiate Prefabs");
                int undoGroup = Undo.GetCurrentGroup();

                for (int i = 0; i < points.Count; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.transform.position = points[i];
                    instance.transform.rotation = Quaternion.identity;
                    if (parent != null)
                        instance.transform.SetParent(parent, true);
                    instance.name = $"{prefab.name}_{i}";
                    Undo.RegisterCreatedObjectUndo(instance, "Poisson Disk - Instantiate");
                    goNames.Add(instance.name);
                }

                Undo.CollapseUndoOperations(undoGroup);
            }

            // --- Build result ---
            var resultPoints = new float[points.Count][];
            for (int i = 0; i < points.Count; i++)
                resultPoints[i] = new[] { points[i].x, points[i].y, points[i].z };

            return ToolResult<ProcGenPoissonDiskResult>.Ok(new ProcGenPoissonDiskResult
            {
                Points          = resultPoints,
                Count           = points.Count,
                GameObjectNames = goNames.Count > 0 ? goNames.ToArray() : null,
                BoundsUsedMin   = bMin,
                BoundsUsedMax   = bMax
            });
        }

        // ----------------------------------------------------------------
        // Bridson 2D — samples on XZ plane, Y from BoundsMin.y
        // ----------------------------------------------------------------
        internal static List<Vector3> SampleBridson2D(float[] bMin, float[] bMax,
            float minDist, float cellSize, System.Random rng, int? maxSamples)
        {
            float width  = bMax[0] - bMin[0];
            float depth  = bMax[2] - bMin[2];
            float y      = bMin[1];

            int gridW = Mathf.CeilToInt(width / cellSize);
            int gridH = Mathf.CeilToInt(depth / cellSize);
            int[] grid = new int[gridW * gridH];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;

            var points     = new List<Vector3>();
            var activeList = new List<int>();

            // First point: center of region
            float fx = bMin[0] + width * 0.5f;
            float fz = bMin[2] + depth * 0.5f;
            AddPoint2D(new Vector3(fx, y, fz), bMin, cellSize, gridW, grid, points, activeList);

            while (activeList.Count > 0)
            {
                if (maxSamples.HasValue && points.Count >= maxSamples.Value)
                    break;

                int idx = rng.Next(activeList.Count);
                var spawn = points[activeList[idx]];
                bool accepted = false;

                for (int k = 0; k < DefaultK; k++)
                {
                    float angle = (float)(rng.NextDouble() * Math.PI * 2);
                    float dist  = (float)(minDist + rng.NextDouble() * minDist);
                    float cx = spawn.x + Mathf.Cos(angle) * dist;
                    float cz = spawn.z + Mathf.Sin(angle) * dist;

                    if (cx < bMin[0] || cx >= bMax[0] || cz < bMin[2] || cz >= bMax[2])
                        continue;

                    int cellX = (int)((cx - bMin[0]) / cellSize);
                    int cellZ = (int)((cz - bMin[2]) / cellSize);

                    if (IsValid2D(cx, cz, cellX, cellZ, gridW, gridH, grid, points, minDist))
                    {
                        AddPoint2D(new Vector3(cx, y, cz), bMin, cellSize, gridW, grid, points, activeList);
                        accepted = true;

                        if (maxSamples.HasValue && points.Count >= maxSamples.Value)
                            break;
                    }
                }

                if (!accepted)
                    activeList.RemoveAt(idx);
            }

            return points;
        }

        private static void AddPoint2D(Vector3 pt, float[] bMin, float cellSize, int gridW,
            int[] grid, List<Vector3> points, List<int> activeList)
        {
            int idx = points.Count;
            points.Add(pt);
            activeList.Add(idx);
            int cellX = (int)((pt.x - bMin[0]) / cellSize);
            int cellZ = (int)((pt.z - bMin[2]) / cellSize);
            grid[cellX + cellZ * gridW] = idx;
        }

        private static bool IsValid2D(float cx, float cz, int cellX, int cellZ,
            int gridW, int gridH, int[] grid, List<Vector3> points, float minDist)
        {
            float minDistSq = minDist * minDist;
            int searchRange = 2;
            for (int dx = -searchRange; dx <= searchRange; dx++)
            {
                for (int dz = -searchRange; dz <= searchRange; dz++)
                {
                    int nx = cellX + dx;
                    int nz = cellZ + dz;
                    if (nx < 0 || nx >= gridW || nz < 0 || nz >= gridH) continue;
                    int pi = grid[nx + nz * gridW];
                    if (pi < 0) continue;
                    float ddx = cx - points[pi].x;
                    float ddz = cz - points[pi].z;
                    if (ddx * ddx + ddz * ddz < minDistSq)
                        return false;
                }
            }
            return true;
        }

        // ----------------------------------------------------------------
        // Bridson 3D — full 3D sampling
        // ----------------------------------------------------------------
        internal static List<Vector3> SampleBridson3D(float[] bMin, float[] bMax,
            float minDist, float cellSize, System.Random rng, int? maxSamples)
        {
            float sizeX = bMax[0] - bMin[0];
            float sizeY = bMax[1] - bMin[1];
            float sizeZ = bMax[2] - bMin[2];

            int gridX = Mathf.CeilToInt(sizeX / cellSize);
            int gridY = Mathf.CeilToInt(sizeY / cellSize);
            int gridZ = Mathf.CeilToInt(sizeZ / cellSize);
            int[] grid = new int[gridX * gridY * gridZ];
            for (int i = 0; i < grid.Length; i++) grid[i] = -1;

            var points     = new List<Vector3>();
            var activeList = new List<int>();

            // First point: center of bounds
            var first = new Vector3(
                bMin[0] + sizeX * 0.5f,
                bMin[1] + sizeY * 0.5f,
                bMin[2] + sizeZ * 0.5f);
            AddPoint3D(first, bMin, cellSize, gridX, gridY, grid, points, activeList);

            while (activeList.Count > 0)
            {
                if (maxSamples.HasValue && points.Count >= maxSamples.Value)
                    break;

                int idx = rng.Next(activeList.Count);
                var spawn = points[activeList[idx]];
                bool accepted = false;

                for (int k = 0; k < DefaultK; k++)
                {
                    // Random point in spherical shell [minDist, 2*minDist]
                    float theta = (float)(rng.NextDouble() * Math.PI * 2);
                    float phi   = (float)(Math.Acos(1 - 2 * rng.NextDouble()));
                    float dist  = (float)(minDist + rng.NextDouble() * minDist);

                    float cx = spawn.x + Mathf.Sin(phi) * Mathf.Cos(theta) * dist;
                    float cy = spawn.y + Mathf.Sin(phi) * Mathf.Sin(theta) * dist;
                    float cz = spawn.z + Mathf.Cos(phi) * dist;

                    if (cx < bMin[0] || cx >= bMax[0] ||
                        cy < bMin[1] || cy >= bMax[1] ||
                        cz < bMin[2] || cz >= bMax[2])
                        continue;

                    int gx = (int)((cx - bMin[0]) / cellSize);
                    int gy = (int)((cy - bMin[1]) / cellSize);
                    int gz = (int)((cz - bMin[2]) / cellSize);

                    if (IsValid3D(cx, cy, cz, gx, gy, gz, gridX, gridY, gridZ, grid, points, minDist))
                    {
                        AddPoint3D(new Vector3(cx, cy, cz), bMin, cellSize, gridX, gridY, grid, points, activeList);
                        accepted = true;

                        if (maxSamples.HasValue && points.Count >= maxSamples.Value)
                            break;
                    }
                }

                if (!accepted)
                    activeList.RemoveAt(idx);
            }

            return points;
        }

        private static void AddPoint3D(Vector3 pt, float[] bMin, float cellSize,
            int gridX, int gridY, int[] grid, List<Vector3> points, List<int> activeList)
        {
            int idx = points.Count;
            points.Add(pt);
            activeList.Add(idx);
            int gx = (int)((pt.x - bMin[0]) / cellSize);
            int gy = (int)((pt.y - bMin[1]) / cellSize);
            int gz = (int)((pt.z - bMin[2]) / cellSize);
            grid[gx + gy * gridX + gz * gridX * gridY] = idx;
        }

        private static bool IsValid3D(float cx, float cy, float cz,
            int gx, int gy, int gz, int gridX, int gridY, int gridZ,
            int[] grid, List<Vector3> points, float minDist)
        {
            float minDistSq = minDist * minDist;
            int searchRange = 2;
            for (int dx = -searchRange; dx <= searchRange; dx++)
            for (int dy = -searchRange; dy <= searchRange; dy++)
            for (int dz = -searchRange; dz <= searchRange; dz++)
            {
                int nx = gx + dx;
                int ny = gy + dy;
                int nz = gz + dz;
                if (nx < 0 || nx >= gridX || ny < 0 || ny >= gridY || nz < 0 || nz >= gridZ) continue;
                int pi = grid[nx + ny * gridX + nz * gridX * gridY];
                if (pi < 0) continue;
                float ddx = cx - points[pi].x;
                float ddy = cy - points[pi].y;
                float ddz = cz - points[pi].z;
                if (ddx * ddx + ddy * ddy + ddz * ddz < minDistSq)
                    return false;
            }
            return true;
        }
    }
}
