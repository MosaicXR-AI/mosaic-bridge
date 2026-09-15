using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

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
                    "IMPORTANT: For rain use RenderMode=Stretch, VelocityScale=0.8, LengthScale=3, MaxParticleSize=0.5, UseUrpParticlesMaterial=true. " +
                    "MeshPath (RenderMode=Mesh), TrailMaterialPath (for the Trails module), SortingLayer/SortingOrder (2D courses).",
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
                // Try shaders in priority order — works across URP, HDRP, and Built-in
                string[] candidates =
                {
                    "Universal Render Pipeline/Particles/Unlit",
                    "Universal Render Pipeline/Particles/Lit",
                    "Particles/Standard Unlit",
                    "Particles/Standard Surface",
                    "Unlit/Color",
                };
                UnityEngine.Shader particleShader = null;
                foreach (var c in candidates)
                {
                    particleShader = UnityEngine.Shader.Find(c);
                    if (particleShader != null) break;
                }
                if (particleShader == null)
                    return ToolResult<ParticleSetRendererResult>.Fail(
                        "No particle-compatible shader found in this project. " +
                        "Ensure a render pipeline package (URP/HDRP) or Particle shaders are installed.",
                        ErrorCodes.NOT_FOUND);

                // L12: `new Material(...)` alone is a pure in-memory object with no asset behind
                // it — it reads back as a missing/pink material the moment the project reloads,
                // because nothing ever wrote it to disk. Save it as a real asset (reusing one
                // already there so repeated calls with the same shader don't create duplicates).
                string safeShaderName = particleShader.name.Replace('/', '-').Replace('\\', '-');
                string matAssetPath = $"Assets/Generated/ParticleMaterials/{safeShaderName}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);
                if (mat == null)
                {
                    mat = new Material(particleShader);
                    if (mat.HasProperty("_Surface"))
                        mat.SetFloat("_Surface", 1f); // Transparent where supported
                    var matDir = Path.GetDirectoryName(matAssetPath);
                    if (!string.IsNullOrEmpty(matDir) && !AssetDatabase.IsValidFolder(matDir))
                        Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", matDir));
                    AssetDatabase.CreateAsset(mat, matAssetPath);
                    AssetDatabase.SaveAssets();
                }
                renderer.sharedMaterial = mat;
            }
            else if (!string.IsNullOrEmpty(p.MaterialPath))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p.MaterialPath);
                if (mat == null)
                    return ToolResult<ParticleSetRendererResult>.Fail(
                        $"Material not found at '{p.MaterialPath}'", ErrorCodes.NOT_FOUND);
                renderer.sharedMaterial = mat;
            }

            if (!string.IsNullOrEmpty(p.MeshPath))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(p.MeshPath);
                if (mesh == null)
                    return ToolResult<ParticleSetRendererResult>.Fail(
                        $"Mesh not found at '{p.MeshPath}'", ErrorCodes.NOT_FOUND);
                renderer.mesh = mesh;
            }

            if (!string.IsNullOrEmpty(p.TrailMaterialPath))
            {
                var trailMat = AssetDatabase.LoadAssetAtPath<Material>(p.TrailMaterialPath);
                if (trailMat == null)
                    return ToolResult<ParticleSetRendererResult>.Fail(
                        $"Trail material not found at '{p.TrailMaterialPath}'", ErrorCodes.NOT_FOUND);
                renderer.trailMaterial = trailMat;
            }

            if (!string.IsNullOrEmpty(p.SortingLayer))
                renderer.sortingLayerName = p.SortingLayer;
            if (p.SortingOrder.HasValue)
                renderer.sortingOrder = p.SortingOrder.Value;

            EditorUtility.SetDirty(renderer);

            string matPath = renderer.sharedMaterial != null
                ? AssetDatabase.GetAssetPath(renderer.sharedMaterial)
                : null;

            return ToolResult<ParticleSetRendererResult>.Ok(new ParticleSetRendererResult
            {
                InstanceId     = UnityIds.Of(ps.gameObject),
                Name           = ps.gameObject.name,
                RenderMode     = renderer.renderMode.ToString(),
                VelocityScale  = renderer.velocityScale,
                LengthScale    = renderer.lengthScale,
                MaxParticleSize = renderer.maxParticleSize,
                MinParticleSize = renderer.minParticleSize,
                MaterialPath   = matPath,
                SortMode       = renderer.sortMode.ToString(),
                MeshPath       = renderer.mesh != null ? AssetDatabase.GetAssetPath(renderer.mesh) : null,
                TrailMaterialPath = renderer.trailMaterial != null ? AssetDatabase.GetAssetPath(renderer.trailMaterial) : null,
                SortingLayer   = renderer.sortingLayerName,
                SortingOrder   = renderer.sortingOrder,
            });
        }
    }
}
