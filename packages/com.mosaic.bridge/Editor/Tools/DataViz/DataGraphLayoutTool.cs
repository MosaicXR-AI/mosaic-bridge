using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// data/graph-layout — compute 2D/3D graph layout positions.
    /// Supports Fruchterman-Reingold force-directed, circular, grid, and edge-only spring layouts.
    /// Deterministic when Seed is provided. Optionally spawns node spheres + edge LineRenderers. Story 34-5.
    /// </summary>
    public static class DataGraphLayoutTool
    {
        static readonly string[] ValidAlgorithms = { "force_directed", "circular", "grid", "spring" };

        [MosaicTool("data/graph-layout",
                    "Computes force-directed / circular / grid / spring graph layout positions in 2D or 3D with optional visual GameObjects",
                    isReadOnly: false, category: "data", Context = ToolContext.Both)]
        public static ToolResult<DataGraphLayoutResult> Execute(DataGraphLayoutParams p)
        {
            p ??= new DataGraphLayoutParams();

            // ---------- Validation ----------
            if (p.Nodes == null || p.Nodes.Count == 0)
                return ToolResult<DataGraphLayoutResult>.Fail(
                    "Nodes must be a non-empty list.", ErrorCodes.INVALID_PARAM);

            string algorithm = string.IsNullOrEmpty(p.Algorithm)
                ? "force_directed"
                : p.Algorithm.Trim().ToLowerInvariant();
            if (Array.IndexOf(ValidAlgorithms, algorithm) < 0)
                return ToolResult<DataGraphLayoutResult>.Fail(
                    $"Invalid Algorithm '{p.Algorithm}'. Valid: force_directed, circular, grid, spring.",
                    ErrorCodes.INVALID_PARAM);

            int iterations    = Mathf.Max(0, p.Iterations ?? 200);
            float idealLength = p.IdealEdgeLength ?? 2.0f;
            if (idealLength <= 0f)
                return ToolResult<DataGraphLayoutResult>.Fail(
                    "IdealEdgeLength must be > 0.", ErrorCodes.INVALID_PARAM);

            float repulsion  = p.Repulsion  ?? 1.0f;
            float attraction = p.Attraction ?? 0.1f;
            float damping    = p.Damping    ?? 0.85f;

            Vector3 bounds = new Vector3(10f, 10f, 10f);
            if (p.Bounds != null)
            {
                if (p.Bounds.Length != 3)
                    return ToolResult<DataGraphLayoutResult>.Fail(
                        "Bounds must have exactly 3 components.", ErrorCodes.INVALID_PARAM);
                bounds = new Vector3(
                    Mathf.Max(0.01f, p.Bounds[0]),
                    Mathf.Max(0.01f, p.Bounds[1]),
                    Mathf.Max(0.01f, p.Bounds[2]));
            }

            bool layout3D     = p.Layout3D     ?? true;
            bool createVisuals = p.CreateVisuals ?? true;
            float nodeSize    = p.NodeSize      ?? 0.3f;
            float edgeThick   = p.EdgeThickness ?? 0.03f;

            Color nodeColor = ArrayToColor(p.NodeColor, new Color(0.3f, 0.7f, 1f, 1f));
            Color edgeColor = ArrayToColor(p.EdgeColor, new Color(0.7f, 0.7f, 0.7f, 0.6f));

            // Build Id -> index map + validate uniqueness.
            var idToIndex = new Dictionary<string, int>(p.Nodes.Count);
            for (int i = 0; i < p.Nodes.Count; i++)
            {
                var node = p.Nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id))
                    return ToolResult<DataGraphLayoutResult>.Fail(
                        $"Nodes[{i}].Id is required.", ErrorCodes.INVALID_PARAM);
                if (idToIndex.ContainsKey(node.Id))
                    return ToolResult<DataGraphLayoutResult>.Fail(
                        $"Duplicate node Id '{node.Id}'.", ErrorCodes.INVALID_PARAM);
                idToIndex[node.Id] = i;
            }

            var edges = p.Edges ?? new List<DataGraphLayoutParams.Edge>();
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                if (e == null || string.IsNullOrEmpty(e.From) || string.IsNullOrEmpty(e.To))
                    return ToolResult<DataGraphLayoutResult>.Fail(
                        $"Edges[{i}] must have both From and To.", ErrorCodes.INVALID_PARAM);
                if (!idToIndex.ContainsKey(e.From))
                    return ToolResult<DataGraphLayoutResult>.Fail(
                        $"Edges[{i}].From '{e.From}' is not a known node Id.", ErrorCodes.INVALID_PARAM);
                if (!idToIndex.ContainsKey(e.To))
                    return ToolResult<DataGraphLayoutResult>.Fail(
                        $"Edges[{i}].To '{e.To}' is not a known node Id.", ErrorCodes.INVALID_PARAM);
            }

            int n = p.Nodes.Count;
            var positions = new Vector3[n];
            var rng = p.Seed.HasValue
                ? new System.Random(p.Seed.Value)
                : new System.Random();

            // ---------- Initial positions ----------
            for (int i = 0; i < n; i++)
            {
                positions[i] = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * bounds.x,
                    layout3D ? (float)(rng.NextDouble() * 2.0 - 1.0) * bounds.y : 0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * bounds.z);
            }

            int iterationsRun = 0;

            switch (algorithm)
            {
                case "circular":
                    LayoutCircular(positions, bounds, layout3D);
                    break;
                case "grid":
                    LayoutGrid(positions, idealLength, layout3D);
                    break;
                case "spring":
                    iterationsRun = LayoutForceDirected(positions, edges, idToIndex,
                        iterations, idealLength, repulsion, attraction, damping,
                        bounds, layout3D, includeRepulsion: false);
                    break;
                case "force_directed":
                default:
                    iterationsRun = LayoutForceDirected(positions, edges, idToIndex,
                        iterations, idealLength, repulsion, attraction, damping,
                        bounds, layout3D, includeRepulsion: true);
                    break;
            }

            // ---------- Build node position result ----------
            var nodePositions = new List<DataGraphLayoutResult.NodePosition>(n);
            for (int i = 0; i < n; i++)
            {
                nodePositions.Add(new DataGraphLayoutResult.NodePosition
                {
                    Id = p.Nodes[i].Id,
                    Position = new[] { positions[i].x, positions[i].y, positions[i].z },
                });
            }

            // ---------- Visuals ----------
            string parentName = string.Empty;
            if (createVisuals)
            {
                string baseName = string.IsNullOrWhiteSpace(p.Name) ? "GraphLayout" : p.Name.Trim();
                var parent = new GameObject(baseName);
                Undo.RegisterCreatedObjectUndo(parent, "Graph Layout");

                var nodeMat = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color"))
                {
                    name = baseName + "_NodeMat",
                    color = nodeColor,
                };
                var edgeMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"))
                {
                    name = baseName + "_EdgeMat",
                    color = edgeColor,
                };

                // Nodes
                for (int i = 0; i < n; i++)
                {
                    var node = p.Nodes[i];
                    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.name = $"Node_{node.Id}";
                    sphere.transform.SetParent(parent.transform, false);
                    sphere.transform.localPosition = positions[i];
                    sphere.transform.localScale = Vector3.one * nodeSize;
                    var r = sphere.GetComponent<Renderer>();
                    if (r != null) r.sharedMaterial = nodeMat;
                }

                // Edges
                for (int i = 0; i < edges.Count; i++)
                {
                    var e = edges[i];
                    int fi = idToIndex[e.From];
                    int ti = idToIndex[e.To];

                    var go = new GameObject($"Edge_{e.From}_{e.To}");
                    go.transform.SetParent(parent.transform, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.useWorldSpace = false;
                    lr.SetPosition(0, positions[fi]);
                    lr.SetPosition(1, positions[ti]);
                    lr.startWidth = edgeThick;
                    lr.endWidth   = edgeThick;
                    lr.sharedMaterial = edgeMat;
                    lr.startColor = edgeColor;
                    lr.endColor   = edgeColor;
                }

                parentName = parent.name;
            }

            return ToolResult<DataGraphLayoutResult>.Ok(new DataGraphLayoutResult
            {
                GameObjectName = parentName,
                NodeCount      = n,
                EdgeCount      = edges.Count,
                Algorithm      = algorithm,
                Iterations     = iterationsRun,
                NodePositions  = nodePositions,
            });
        }

        // =========================================================
        // Force-directed (Fruchterman-Reingold, optionally edge-only)
        // =========================================================
        static int LayoutForceDirected(
            Vector3[] positions,
            List<DataGraphLayoutParams.Edge> edges,
            Dictionary<string, int> idToIndex,
            int iterations,
            float idealEdgeLength,
            float repulsion,
            float attraction,
            float damping,
            Vector3 bounds,
            bool layout3D,
            bool includeRepulsion)
        {
            int n = positions.Length;
            if (n == 0 || iterations <= 0) return 0;

            // k = sqrt(area / nodeCount) * idealEdgeLength  (3D uses cube-root-ish area proxy)
            float area = (2f * bounds.x) * (2f * bounds.z) * (layout3D ? (2f * bounds.y) : 1f);
            float k = Mathf.Sqrt(Mathf.Max(1e-4f, area) / Mathf.Max(1, n)) * idealEdgeLength;
            float k2 = k * k;

            // Temperature cools linearly so step size tapers off.
            float tStart = Mathf.Max(bounds.x, Mathf.Max(bounds.y, bounds.z)) * 0.1f;

            var velocity = new Vector3[n];
            var force    = new Vector3[n];

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < n; i++) force[i] = Vector3.zero;

                // Repulsion: F = k^2 / d^2 along direction (scaled by repulsion coefficient).
                if (includeRepulsion)
                {
                    for (int i = 0; i < n; i++)
                    {
                        for (int j = i + 1; j < n; j++)
                        {
                            Vector3 delta = positions[i] - positions[j];
                            float distSq = Mathf.Max(1e-4f, delta.sqrMagnitude);
                            float dist   = Mathf.Sqrt(distSq);
                            float mag    = repulsion * k2 / distSq;
                            Vector3 push = (delta / dist) * mag;
                            force[i] += push;
                            force[j] -= push;
                        }
                    }
                }

                // Attraction along edges: F = d^2 / k * edgeWeight
                for (int i = 0; i < edges.Count; i++)
                {
                    var e = edges[i];
                    int a = idToIndex[e.From];
                    int b = idToIndex[e.To];
                    float w = e.Weight ?? 1f;
                    Vector3 delta = positions[a] - positions[b];
                    float dist = Mathf.Max(1e-4f, delta.magnitude);
                    float mag = attraction * (dist * dist) / k * w;
                    Vector3 pull = (delta / dist) * mag;
                    force[a] -= pull;
                    force[b] += pull;
                }

                float t = tStart * (1f - (float)iter / Mathf.Max(1, iterations));
                t = Mathf.Max(t, tStart * 0.01f);

                for (int i = 0; i < n; i++)
                {
                    velocity[i] = (velocity[i] + force[i]) * damping;
                    Vector3 step = velocity[i];
                    float mag = step.magnitude;
                    if (mag > t) step = (step / mag) * t;

                    positions[i] += step;

                    // Clamp to bounds
                    positions[i] = new Vector3(
                        Mathf.Clamp(positions[i].x, -bounds.x, bounds.x),
                        layout3D ? Mathf.Clamp(positions[i].y, -bounds.y, bounds.y) : 0f,
                        Mathf.Clamp(positions[i].z, -bounds.z, bounds.z));
                }
            }

            if (!layout3D)
            {
                for (int i = 0; i < n; i++)
                    positions[i] = new Vector3(positions[i].x, 0f, positions[i].z);
            }

            return iterations;
        }

        // =========================================================
        // Circular: nodes evenly around a ring in the XZ plane.
        // =========================================================
        static void LayoutCircular(Vector3[] positions, Vector3 bounds, bool layout3D)
        {
            int n = positions.Length;
            if (n == 0) return;
            float radius = Mathf.Max(bounds.x, bounds.z) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f;
                positions[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
        }

        // =========================================================
        // Grid: ceil(sqrt(N)) x ceil(sqrt(N)) grid centered at origin.
        // =========================================================
        static void LayoutGrid(Vector3[] positions, float spacing, bool layout3D)
        {
            int n = positions.Length;
            if (n == 0) return;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(n));
            if (cols < 1) cols = 1;
            int rows = Mathf.CeilToInt((float)n / cols);

            float xOff = -(cols - 1) * spacing * 0.5f;
            float zOff = -(rows - 1) * spacing * 0.5f;

            for (int i = 0; i < n; i++)
            {
                int r = i / cols;
                int c = i % cols;
                positions[i] = new Vector3(
                    xOff + c * spacing,
                    0f,
                    zOff + r * spacing);
            }
        }

        // =========================================================
        // Helpers
        // =========================================================
        static Color ArrayToColor(float[] arr, Color fallback)
        {
            if (arr == null) return fallback;
            if (arr.Length == 3) return new Color(arr[0], arr[1], arr[2], 1f);
            if (arr.Length == 4) return new Color(arr[0], arr[1], arr[2], arr[3]);
            return fallback;
        }
    }
}
