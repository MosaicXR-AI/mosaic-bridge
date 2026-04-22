using System;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Particles
{
    public static class ParticleSetRendererTool
    {
        [MosaicTool("particle/set-renderer",
                    "Sets ParticleSystemRenderer properties: RenderMode (Billboard|Stretch|HorizontalBillboard|VerticalBillboard), " +
                    "VelocityScale (stretch by speed — set to 0.8 for rain streaks), " +
                    "LengthScale (streak length multiplier — 3 for rain), " +
                    "MaxParticleSize (screen-space cap — 0.5 for visible rain, 0.005 makes particles invisible), " +
                    "MaterialPath (asset path to .mat file), " +
                    "UseUrpParticlesMaterial=true to auto-assign Universal Render Pipeline/Particles/Unlit material (required in URP projects). " +
                    "IMPORTANT: For rain use RenderMode=Stretch, VelocityScale=0.8, LengthScale=3, MaxParticleSize=0.5, UseUrpParticlesMaterial=true.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ParticleSetRendererResult> Execute(ParticleSetRendererParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<ParticleSetRendererResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            var ps = ParticleToolHelpers.Resolve(p.InstanceId, p.Name);
            if (ps == null)
                return ToolResult<ParticleSetRendererResult>.Fail(
                    $"ParticleSystem not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                    ErrorCodes.NOT_FOUND);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
                return ToolResult<ParticleSetRendererResult>.Fail(
                    "ParticleSystemRenderer component not found", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(renderer, "Mosaic: Set ParticleSystem Renderer");

            if (!string.IsNullOrEmpty(p.RenderMode))
            {
                if (Enum.TryParse<ParticleSystemRenderMode>(p.RenderMode, true, out var mode))
                    renderer.renderMode = mode;
                else
                    return ToolResult<ParticleSetRendererResult>.Fail(
                        $"Unknown RenderMode '{p.RenderMode}'. Valid: Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh, None",
                        ErrorCodes.INVALID_PARAM);
            }

            if (p.VelocityScale.HasValue)
                renderer.velocityScale = p.VelocityScale.Value;

            if (p.LengthScale.HasValue)
                renderer.lengthScale = p.LengthScale.Value;

            if (p.MaxParticleSize.HasValue)
                renderer.maxParticleSize = p.MaxParticleSize.Value;

            if (p.MinParticleSize.HasValue)
                renderer.minParticleSize = p.MinParticleSize.Value;

            if (!string.IsNullOrEmpty(p.SortMode))
            {
                if (Enum.TryParse<ParticleSystemSortMode>(p.SortMode, true, out var sort))
                    renderer.sortMode = sort;
            }

            if (p.UseUrpParticlesMaterial == true)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                    shader = Shader.Find("Particles/Standard Unlit");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.SetFloat("_Surface", 1f); // Transparent
                    renderer.sharedMaterial = mat;
                }
            }
            else if (!string.IsNullOrEmpty(p.MaterialPath))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p.MaterialPath);
                if (mat == null)
                    return ToolResult<ParticleSetRendererResult>.Fail(
                        $"Material not found at '{p.MaterialPath}'", ErrorCodes.NOT_FOUND);
                renderer.sharedMaterial = mat;
            }

            EditorUtility.SetDirty(renderer);

            string matPath = renderer.sharedMaterial != null
                ? AssetDatabase.GetAssetPath(renderer.sharedMaterial)
                : null;

            return ToolResult<ParticleSetRendererResult>.Ok(new ParticleSetRendererResult
            {
                InstanceId     = ps.gameObject.GetInstanceID(),
                Name           = ps.gameObject.name,
                RenderMode     = renderer.renderMode.ToString(),
                VelocityScale  = renderer.velocityScale,
                LengthScale    = renderer.lengthScale,
                MaxParticleSize = renderer.maxParticleSize,
                MinParticleSize = renderer.minParticleSize,
                MaterialPath   = matPath,
                SortMode       = renderer.sortMode.ToString()
            });
        }
    }
}
