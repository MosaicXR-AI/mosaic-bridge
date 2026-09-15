using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Renderers
{
    public static class RendererLineTool
    {
        // O4 §4.8: laser sights, path previews, grappling hooks, sword trails. component/set_property
        // cannot set Vector3[]/Gradient — this is the dedicated route for LineRenderer's actual
        // authoring surface (positions, width curve, color gradient), lifted from the pattern
        // chart/* already uses internally for axis/edge lines.
        [MosaicTool("renderer/line",
                    "Adds/configures a LineRenderer: Positions (flattened [x,y,z,...] triples, min 2 points), " +
                    "Loop, UseWorldSpace, Start/EndWidth or a WidthCurve, Start/EndColor, MaterialPath, " +
                    "Alignment (View/TransformZ), TextureMode.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<RendererLineResult> Execute(RendererLineParams p)
        {
            var go = RendererToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<RendererLineResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            if (p.Positions == null || p.Positions.Length < 6 || p.Positions.Length % 3 != 0)
                return ToolResult<RendererLineResult>.Fail(
                    "Positions must be a flattened [x,y,z,...] array with at least 2 points (6 floats), " +
                    "and a length that is a multiple of 3", ErrorCodes.INVALID_PARAM);

            bool added = false;
            var lr = go.GetComponent<LineRenderer>();
            if (lr == null)
            {
                lr = Undo.AddComponent<LineRenderer>(go);
                added = true;
            }
            else
            {
                Undo.RecordObject(lr, "Mosaic: Configure LineRenderer");
            }

            int pointCount = p.Positions.Length / 3;
            var points = new Vector3[pointCount];
            for (int i = 0; i < pointCount; i++)
                points[i] = new Vector3(p.Positions[i * 3], p.Positions[i * 3 + 1], p.Positions[i * 3 + 2]);

            lr.useWorldSpace = p.UseWorldSpace;
            lr.loop = p.Loop;
            lr.positionCount = pointCount;
            lr.SetPositions(points);

            if (p.StartWidth.HasValue) lr.startWidth = p.StartWidth.Value;
            if (p.EndWidth.HasValue) lr.endWidth = p.EndWidth.Value;

            if (p.WidthCurveTimes != null || p.WidthCurveValues != null)
            {
                if (p.WidthCurveTimes == null || p.WidthCurveValues == null ||
                    p.WidthCurveTimes.Length != p.WidthCurveValues.Length || p.WidthCurveTimes.Length == 0)
                    return ToolResult<RendererLineResult>.Fail(
                        "WidthCurveTimes/WidthCurveValues must be non-empty and the same length", ErrorCodes.INVALID_PARAM);

                var keys = new Keyframe[p.WidthCurveTimes.Length];
                for (int i = 0; i < keys.Length; i++)
                    keys[i] = new Keyframe(p.WidthCurveTimes[i], p.WidthCurveValues[i]);
                lr.widthCurve = new AnimationCurve(keys);
            }

            if (p.StartColor != null || p.EndColor != null)
            {
                var startColor = RendererToolHelpers.ParseColor(p.StartColor, lr.startColor);
                var endColor = RendererToolHelpers.ParseColor(p.EndColor, lr.endColor);
                lr.startColor = startColor;
                lr.endColor = endColor;
                lr.colorGradient = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(endColor.a, 1f) },
                };
            }

            if (!string.IsNullOrEmpty(p.MaterialPath))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(p.MaterialPath);
                if (material == null)
                    return ToolResult<RendererLineResult>.Fail(
                        $"Material not found at '{p.MaterialPath}'", ErrorCodes.NOT_FOUND);
                lr.sharedMaterial = material;
            }

            if (!string.IsNullOrEmpty(p.Alignment))
            {
                if (!System.Enum.TryParse<LineAlignment>(p.Alignment, ignoreCase: true, out var alignment))
                    return ToolResult<RendererLineResult>.Fail(
                        $"Unknown Alignment '{p.Alignment}'. Valid: View, TransformZ", ErrorCodes.INVALID_PARAM);
                lr.alignment = alignment;
            }

            if (!string.IsNullOrEmpty(p.TextureMode))
            {
                if (!System.Enum.TryParse<LineTextureMode>(p.TextureMode, ignoreCase: true, out var textureMode))
                    return ToolResult<RendererLineResult>.Fail(
                        $"Unknown TextureMode '{p.TextureMode}'. Valid: Stretch, Tile, DistributePerSegment, RepeatPerSegment",
                        ErrorCodes.INVALID_PARAM);
                lr.textureMode = textureMode;
            }

            return ToolResult<RendererLineResult>.Ok(new RendererLineResult
            {
                GameObjectName = go.name,
                InstanceId     = UnityIds.Of(go),
                PositionCount  = lr.positionCount,
                Loop           = lr.loop,
                ComponentAdded = added,
                Message        = $"{(added ? "Added" : "Configured")} LineRenderer with {pointCount} point(s) on '{go.name}'.",
            });
        }
    }
}
