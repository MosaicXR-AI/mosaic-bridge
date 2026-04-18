using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// chart/network - Creates a 3D graph visualization from nodes and edges.
    /// Nodes without explicit positions are laid out via a simple force-directed
    /// algorithm (Fruchterman-Reingold style) when AutoLayout is enabled.
    /// </summary>
    public static class ChartNetworkTool
    {
        [MosaicTool("chart/network",
                    "Creates a 3D network/graph visualization with spheres for nodes and LineRenderers for edges, optional force-directed auto-layout",
                    isReadOnly: false, category: "chart", Context = ToolContext.Both)]
        public static ToolResult<ChartNetworkResult> Execute(ChartNetworkParams p)
        {
            if (p == null)
                return ToolResult<ChartNetworkResult>.Fail("Parameters required", ErrorCodes.INVALID_PARAM);
            if (p.Nodes == null || p.Nodes.Count == 0)
                return ToolResult<ChartNetworkResult>.Fail("Nodes must be a non-empty list", ErrorCodes.INVALID_PARAM);

            bool autoLayout = p.AutoLayout ?? true;
            int  iterations = p.LayoutIterations ?? 100;

            // Validate node ids and build lookup
            var idToIndex = new Dictionary<string, int>();
            for (int i = 0; i < p.Nodes.Count; i++)
            {
                var n = p.Nodes[i];
                if (n == null || string.IsNullOrEmpty(n.Id))
                    return ToolResult<ChartNetworkResult>.Fail(
                        $"Nodes[{i}].Id is required", ErrorCodes.INVALID_PARAM);
                if (idToIndex.ContainsKey(n.Id))
                    return ToolResult<ChartNetworkResult>.Fail(
                        $"Duplicate node Id '{n.Id}'", ErrorCodes.INVALID_PARAM);
                idToIndex[n.Id] = i;
            }

            var edges = p.Edges ?? new List<ChartNetworkParams.Edge>();
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                if (e == null || string.IsNullOrEmpty(e.From) || string.IsNullOrEmpty(e.To))
                    return ToolResult<ChartNetworkResult>.Fail(
                        $"Edges[{i}] must have From and To", ErrorCodes.INVALID_PARAM);
                if (!idToIndex.ContainsKey(e.From))
                    return ToolResult<ChartNetworkResult>.Fail(
                        $"Edge[{i}].From '{e.From}' is not a known node Id", ErrorCodes.INVALID_PARAM);
                if (!idToIndex.ContainsKey(e.To))
                    return ToolResult<ChartNetworkResult>.Fail(
                        $"Edge[{i}].To '{e.To}' is not a known node Id", ErrorCodes.INVALID_PARAM);
            }

            // Resolve positions
            var positions = new Vector3[p.Nodes.Count];
            bool anyMissing = false;
            for (int i = 0; i < p.Nodes.Count; i++)
            {
                var n = p.Nodes[i];
                if (n.Position != null && n.Position.Length == 3)
                {
                    positions[i] = new Vector3(n.Position[0], n.Position[1], n.Position[2]);
                }
                else
                {
                    anyMissing = true;
                    // Seed on a sphere to avoid overlap
                    float phi   = (i * 2.3999632f); // golden angle
                    float y     = 1f - (i / (float)Mathf.Max(1, p.Nodes.Count - 1)) * 2f;
                    float r     = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    positions[i] = new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r) * 3f;
                }
            }

            if (anyMissing && autoLayout)
            {
                RunForceDirected(positions, edges, idToIndex, iterations);
            }

            Vector3 origin = ChartScatterTool.ToVec3(p.Position, Vector3.zero);
            string name = string.IsNullOrEmpty(p.Name) ? "NetworkChart" : p.Name;

            var parent = new GameObject(name);
            parent.transform.position = origin;
            Undo.RegisterCreatedObjectUndo(parent, "Chart Network");

            // Nodes
            var nodeGos = new GameObject[p.Nodes.Count];
            for (int i = 0; i < p.Nodes.Count; i++)
            {
                var n = p.Nodes[i];
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Node_{n.Id}";
                sphere.transform.SetParent(parent.transform, false);
                sphere.transform.localPosition = positions[i];
                float size = n.Size ?? 0.5f;
                sphere.transform.localScale = Vector3.one * size;

                var r = sphere.GetComponent<Renderer>();
                if (r != null)
                    r.sharedMaterial = ChartScatterTool.CreateMaterial(
                        ChartScatterTool.ToColor(n.Color, Color.yellow));

                if (!string.IsNullOrEmpty(n.Label))
                {
                    ChartScatterTool.CreateLabel(parent.transform, n.Label,
                        positions[i] + Vector3.up * (size * 0.75f + 0.15f),
                        $"Label_{n.Id}");
                }

                nodeGos[i] = sphere;
            }

            // Edges
            for (int i = 0; i < edges.Count; i++)
            {
                var e = edges[i];
                int fi = idToIndex[e.From];
                int ti = idToIndex[e.To];
                float w = e.Weight ?? 1f;

                var go = new GameObject($"Edge_{e.From}_{e.To}");
                go.transform.SetParent(parent.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.SetPosition(0, positions[fi]);
                lr.SetPosition(1, positions[ti]);
                float thickness = Mathf.Max(0.005f, w * 0.02f);
                lr.startWidth = thickness;
                lr.endWidth   = thickness;
                lr.useWorldSpace = false;
                var col = ChartScatterTool.ToColor(e.Color, Color.gray);
                lr.sharedMaterial = ChartScatterTool.CreateMaterial(col);
                lr.startColor = col;
                lr.endColor   = col;
            }

            return ToolResult<ChartNetworkResult>.Ok(new ChartNetworkResult
            {
                GameObjectName = parent.name,
                InstanceId     = parent.GetInstanceID(),
                NodeCount      = p.Nodes.Count,
                EdgeCount      = edges.Count
            });
        }

        // Simple Fruchterman-Reingold-style layout in 3D.
        private static void RunForceDirected(
            Vector3[] positions,
            List<ChartNetworkParams.Edge> edges,
            Dictionary<string, int> idToIndex,
            int iterations)
        {
            int n = positions.Length;
            float area = 10f;
            float k = Mathf.Pow(area * area * area / Mathf.Max(1, n), 1f / 3f);
            float t = area * 0.1f; // initial "temperature"
            var disp = new Vector3[n];

            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < n; i++) disp[i] = Vector3.zero;

                // Repulsion
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        Vector3 delta = positions[i] - positions[j];
                        float dist = Mathf.Max(0.01f, delta.magnitude);
                        float force = (k * k) / dist;
                        Vector3 d = (delta / dist) * force;
                        disp[i] += d;
                        disp[j] -= d;
                    }
                }

                // Attraction along edges
                foreach (var e in edges)
                {
                    int a = idToIndex[e.From];
                    int b = idToIndex[e.To];
                    Vector3 delta = positions[a] - positions[b];
                    float dist = Mathf.Max(0.01f, delta.magnitude);
                    float force = (dist * dist) / k;
                    Vector3 d = (delta / dist) * force;
                    disp[a] -= d;
                    disp[b] += d;
                }

                // Apply displacement with temperature cap
                for (int i = 0; i < n; i++)
                {
                    float mag = disp[i].magnitude;
                    if (mag > 1e-6f)
                    {
                        Vector3 step = (disp[i] / mag) * Mathf.Min(mag, t);
                        positions[i] += step;
                    }
                }

                // Cool
                t *= 0.95f;
            }
        }
    }
}
