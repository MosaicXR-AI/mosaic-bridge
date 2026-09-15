using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Renderers
{
    public static class RendererTrailTool
    {
        [MosaicTool("renderer/trail",
                    "Adds/configures a TrailRenderer: Time, MinVertexDistance, Emitting, Autodestruct, " +
                    "Start/EndWidth, Start/EndColor, MaterialPath.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<RendererTrailResult> Execute(RendererTrailParams p)
        {
            var go = RendererToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<RendererTrailResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            bool added = false;
            var tr = go.GetComponent<TrailRenderer>();
            if (tr == null)
            {
                tr = Undo.AddComponent<TrailRenderer>(go);
                added = true;
            }
            else
            {
                Undo.RecordObject(tr, "Mosaic: Configure TrailRenderer");
            }

            if (p.Time.HasValue) tr.time = p.Time.Value;
            if (p.MinVertexDistance.HasValue) tr.minVertexDistance = p.MinVertexDistance.Value;
            if (p.Emitting.HasValue) tr.emitting = p.Emitting.Value;
            if (p.Autodestruct.HasValue) tr.autodestruct = p.Autodestruct.Value;
            if (p.StartWidth.HasValue) tr.startWidth = p.StartWidth.Value;
            if (p.EndWidth.HasValue) tr.endWidth = p.EndWidth.Value;

            if (p.StartColor != null || p.EndColor != null)
            {
                var startColor = RendererToolHelpers.ParseColor(p.StartColor, tr.startColor);
                var endColor = RendererToolHelpers.ParseColor(p.EndColor, tr.endColor);
                tr.startColor = startColor;
                tr.endColor = endColor;
                tr.colorGradient = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(endColor.a, 1f) },
                };
            }

            if (!string.IsNullOrEmpty(p.MaterialPath))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(p.MaterialPath);
                if (material == null)
                    return ToolResult<RendererTrailResult>.Fail(
                        $"Material not found at '{p.MaterialPath}'", ErrorCodes.NOT_FOUND);
                tr.sharedMaterial = material;
            }

            return ToolResult<RendererTrailResult>.Ok(new RendererTrailResult
            {
                GameObjectName    = go.name,
                InstanceId        = UnityIds.Of(go),
                Time              = tr.time,
                MinVertexDistance = tr.minVertexDistance,
                Emitting          = tr.emitting,
                Autodestruct      = tr.autodestruct,
                ComponentAdded    = added,
                Message           = $"{(added ? "Added" : "Configured")} TrailRenderer on '{go.name}'.",
            });
        }
    }
}
