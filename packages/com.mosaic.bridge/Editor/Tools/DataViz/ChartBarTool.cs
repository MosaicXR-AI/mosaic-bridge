using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// chart/bar - Creates a 3D bar chart GameObject from a list of bars.
    /// Values are auto-normalized so the tallest bar matches MaxHeight.
    /// </summary>
    public static class ChartBarTool
    {
        [MosaicTool("chart/bar",
                    "Creates a 3D bar chart GameObject from a list of bars with auto-scaled heights and optional labels/axes",
                    isReadOnly: false, category: "chart", Context = ToolContext.Both)]
        public static ToolResult<ChartBarResult> Execute(ChartBarParams p)
        {
            if (p == null)
                return ToolResult<ChartBarResult>.Fail("Parameters required", ErrorCodes.INVALID_PARAM);
            if (p.Bars == null || p.Bars.Count == 0)
                return ToolResult<ChartBarResult>.Fail("Bars must be a non-empty list", ErrorCodes.INVALID_PARAM);

            float barWidth   = p.BarWidth   ?? 0.8f;
            float barSpacing = p.BarSpacing ?? 1.0f;
            float maxHeight  = p.MaxHeight  ?? 10f;
            bool  showAxes   = p.ShowAxes   ?? true;
            bool  showLabels = p.ShowLabels ?? true;

            if (barWidth <= 0f)
                return ToolResult<ChartBarResult>.Fail("BarWidth must be > 0", ErrorCodes.INVALID_PARAM);
            if (barSpacing <= 0f)
                return ToolResult<ChartBarResult>.Fail("BarSpacing must be > 0", ErrorCodes.INVALID_PARAM);
            if (maxHeight <= 0f)
                return ToolResult<ChartBarResult>.Fail("MaxHeight must be > 0", ErrorCodes.INVALID_PARAM);

            // Compute scale factor from largest absolute value
            float maxVal = 0f;
            for (int i = 0; i < p.Bars.Count; i++)
            {
                var b = p.Bars[i];
                if (b == null)
                    return ToolResult<ChartBarResult>.Fail($"Bars[{i}] is null", ErrorCodes.INVALID_PARAM);
                float av = Mathf.Abs(b.Value);
                if (av > maxVal) maxVal = av;
            }
            float scale = maxVal > 1e-6f ? maxHeight / maxVal : 1f;

            Vector3 origin = ChartScatterTool.ToVec3(p.Position, Vector3.zero);
            string name = string.IsNullOrEmpty(p.Name) ? "BarChart" : p.Name;

            var parent = new GameObject(name);
            parent.transform.position = origin;
            Undo.RegisterCreatedObjectUndo(parent, "Chart Bar");

            float totalWidth = (p.Bars.Count - 1) * barSpacing;

            for (int i = 0; i < p.Bars.Count; i++)
            {
                var b = p.Bars[i];
                float h = b.Value * scale;
                float x = i * barSpacing;

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Bar_{i}";
                cube.transform.SetParent(parent.transform, false);
                cube.transform.localPosition = new Vector3(x, h * 0.5f, 0f);
                cube.transform.localScale = new Vector3(barWidth, Mathf.Abs(h), barWidth);

                var r = cube.GetComponent<Renderer>();
                if (r != null)
                    r.sharedMaterial = ChartScatterTool.CreateMaterial(
                        ChartScatterTool.ToColor(b.Color, Color.cyan));

                if (showLabels && !string.IsNullOrEmpty(b.Label))
                {
                    ChartScatterTool.CreateLabel(parent.transform, b.Label,
                        new Vector3(x, -0.4f, 0f), $"Label_{i}");
                }
            }

            if (showAxes)
            {
                ChartScatterTool.CreateAxis(parent.transform, "AxisX",
                    new Vector3(-barSpacing * 0.5f, 0f, 0f),
                    new Vector3(totalWidth + barSpacing * 0.5f, 0f, 0f),
                    Color.red, "X");
                ChartScatterTool.CreateAxis(parent.transform, "AxisY",
                    Vector3.zero,
                    new Vector3(0f, maxHeight, 0f),
                    Color.green, "Y");
            }

            return ToolResult<ChartBarResult>.Ok(new ChartBarResult
            {
                GameObjectName = parent.name,
                InstanceId     = parent.GetInstanceID(),
                BarCount       = p.Bars.Count
            });
        }
    }
}
