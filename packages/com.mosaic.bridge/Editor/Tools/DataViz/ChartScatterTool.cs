using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// chart/scatter - Creates a 3D scatter plot GameObject from a list of points.
    /// Optionally draws axes, grid, and labels; auto-scales into a 10x10x10 cube.
    /// </summary>
    public static class ChartScatterTool
    {
        private const float ChartSize = 10f;

        [MosaicTool("chart/scatter",
                    "Creates a 3D scatter plot GameObject from a list of points with optional axes, grid, and labels",
                    isReadOnly: false, category: "chart", Context = ToolContext.Both)]
        public static ToolResult<ChartScatterResult> Execute(ChartScatterParams p)
        {
            if (p == null)
                return ToolResult<ChartScatterResult>.Fail("Parameters required", ErrorCodes.INVALID_PARAM);
            if (p.Points == null || p.Points.Count == 0)
                return ToolResult<ChartScatterResult>.Fail("Points must be a non-empty list", ErrorCodes.INVALID_PARAM);

            bool showAxes   = p.ShowAxes   ?? true;
            bool showGrid   = p.ShowGrid   ?? false;
            bool showLabels = p.ShowLabels ?? false;
            bool autoScale  = p.AutoScale  ?? true;

            // Validate points
            for (int i = 0; i < p.Points.Count; i++)
            {
                var pt = p.Points[i];
                if (pt == null || pt.Position == null || pt.Position.Length != 3)
                    return ToolResult<ChartScatterResult>.Fail(
                        $"Point[{i}].Position must be a float[3]", ErrorCodes.INVALID_PARAM);
            }

            string[] axisLabels = p.AxisLabels != null && p.AxisLabels.Length == 3
                ? p.AxisLabels : new[] { "X", "Y", "Z" };

            Vector3 origin = ToVec3(p.Position, Vector3.zero);
            string name = string.IsNullOrEmpty(p.Name) ? "ScatterChart" : p.Name;

            var parent = new GameObject(name);
            parent.transform.position = origin;
            Undo.RegisterCreatedObjectUndo(parent, "Chart Scatter");

            // Compute bounds for auto-scale
            Vector3 minB = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 maxB = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (var pt in p.Points)
            {
                var v = new Vector3(pt.Position[0], pt.Position[1], pt.Position[2]);
                minB = Vector3.Min(minB, v);
                maxB = Vector3.Max(maxB, v);
            }
            Vector3 range = maxB - minB;
            if (range.x < 1e-6f) range.x = 1f;
            if (range.y < 1e-6f) range.y = 1f;
            if (range.z < 1e-6f) range.z = 1f;

            Vector3 Map(Vector3 v)
            {
                if (!autoScale) return v;
                return new Vector3(
                    (v.x - minB.x) / range.x * ChartSize,
                    (v.y - minB.y) / range.y * ChartSize,
                    (v.z - minB.z) / range.z * ChartSize);
            }

            // Spheres
            for (int i = 0; i < p.Points.Count; i++)
            {
                var pt = p.Points[i];
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Point_{i}";
                sphere.transform.SetParent(parent.transform, false);
                var pos = Map(new Vector3(pt.Position[0], pt.Position[1], pt.Position[2]));
                sphere.transform.localPosition = pos;

                float size = pt.Size ?? 0.1f;
                sphere.transform.localScale = Vector3.one * size;

                var r = sphere.GetComponent<Renderer>();
                if (r != null)
                    r.sharedMaterial = CreateMaterial(ToColor(pt.Color, Color.cyan));

                if (showLabels && !string.IsNullOrEmpty(pt.Label))
                {
                    CreateLabel(parent.transform, pt.Label,
                        pos + Vector3.up * (size * 0.75f + 0.1f), $"Label_{i}");
                }
            }

            if (showAxes)
            {
                CreateAxis(parent.transform, "AxisX", Vector3.zero, Vector3.right * ChartSize, Color.red, axisLabels[0]);
                CreateAxis(parent.transform, "AxisY", Vector3.zero, Vector3.up    * ChartSize, Color.green, axisLabels[1]);
                CreateAxis(parent.transform, "AxisZ", Vector3.zero, Vector3.forward * ChartSize, Color.blue, axisLabels[2]);
            }

            if (showGrid)
                CreateGrid(parent.transform);

            return ToolResult<ChartScatterResult>.Ok(new ChartScatterResult
            {
                GameObjectName = parent.name,
                InstanceId     = parent.GetInstanceID(),
                PointCount     = p.Points.Count
            });
        }

        // -----------------------------------------------------------------
        internal static Vector3 ToVec3(float[] a, Vector3 dflt)
        {
            if (a == null || a.Length != 3) return dflt;
            return new Vector3(a[0], a[1], a[2]);
        }

        internal static Color ToColor(float[] c, Color dflt)
        {
            if (c == null || c.Length < 3) return dflt;
            float a = c.Length >= 4 ? c[3] : 1f;
            return new Color(c[0], c[1], c[2], a);
        }

        internal static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        internal static void CreateLabel(Transform parent, string text, Vector3 pos, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 32;
            tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
        }

        internal static void CreateAxis(Transform parent, string name, Vector3 from, Vector3 to, Color color, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.03f;
            lr.endWidth   = 0.03f;
            lr.useWorldSpace = false;
            lr.sharedMaterial = CreateMaterial(color);
            lr.startColor = color;
            lr.endColor   = color;

            // Arrowhead as a small cone-approximating cube at the tip
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = name + "_Tip";
            tip.transform.SetParent(go.transform, false);
            tip.transform.localPosition = to;
            tip.transform.localScale = Vector3.one * 0.15f;
            var r = tip.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = CreateMaterial(color);

            if (!string.IsNullOrEmpty(label))
                CreateLabel(go.transform, label, to + (to.normalized * 0.4f), name + "_Label");
        }

        internal static void CreateGrid(Transform parent)
        {
            var grid = new GameObject("Grid");
            grid.transform.SetParent(parent, false);
            int steps = 10;
            float step = ChartSize / steps;
            var mat = CreateMaterial(new Color(0.4f, 0.4f, 0.4f, 0.5f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i * step;
                // Lines on XZ plane
                var lx = new GameObject($"GridX_{i}");
                lx.transform.SetParent(grid.transform, false);
                var lrx = lx.AddComponent<LineRenderer>();
                lrx.positionCount = 2;
                lrx.SetPosition(0, new Vector3(0, 0, t));
                lrx.SetPosition(1, new Vector3(ChartSize, 0, t));
                lrx.startWidth = 0.01f; lrx.endWidth = 0.01f;
                lrx.useWorldSpace = false;
                lrx.sharedMaterial = mat;

                var lz = new GameObject($"GridZ_{i}");
                lz.transform.SetParent(grid.transform, false);
                var lrz = lz.AddComponent<LineRenderer>();
                lrz.positionCount = 2;
                lrz.SetPosition(0, new Vector3(t, 0, 0));
                lrz.SetPosition(1, new Vector3(t, 0, ChartSize));
                lrz.startWidth = 0.01f; lrz.endWidth = 0.01f;
                lrz.useWorldSpace = false;
                lrz.sharedMaterial = mat;
            }
        }
    }
}
