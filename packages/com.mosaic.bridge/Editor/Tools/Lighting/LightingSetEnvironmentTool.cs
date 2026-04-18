using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Lighting
{
    public static class LightingSetEnvironmentTool
    {
        [MosaicTool("lighting/set-environment",
                    "Sets environment lighting, ambient, skybox, and fog settings",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<LightingSetEnvironmentResult> Execute(LightingSetEnvironmentParams p)
        {
            // RenderSettings is a singleton — use Undo.RegisterCompleteObjectUndo on the
            // internal object retrieved via Resources.FindObjectsOfTypeAll
            var rsArray = Resources.FindObjectsOfTypeAll<RenderSettings>();
            if (rsArray.Length > 0)
                Undo.RecordObject(rsArray[0], "Mosaic: Set Environment Lighting");

            int changed = 0;

            if (!string.IsNullOrEmpty(p.AmbientMode))
            {
                if (Enum.TryParse<AmbientMode>(p.AmbientMode, true, out var mode))
                {
                    RenderSettings.ambientMode = mode;
                    changed++;
                }
                else
                {
                    return ToolResult<LightingSetEnvironmentResult>.Fail(
                        $"Invalid ambient mode '{p.AmbientMode}'. Valid modes: Skybox, Trilight, Flat, Custom",
                        ErrorCodes.INVALID_PARAM);
                }
            }

            if (p.AmbientColor != null)
            {
                var color = LightingToolHelpers.ParseColor(p.AmbientColor);
                if (color.HasValue)
                {
                    RenderSettings.ambientLight = color.Value;
                    changed++;
                }
            }

            if (p.AmbientIntensity.HasValue)
            {
                RenderSettings.ambientIntensity = p.AmbientIntensity.Value;
                changed++;
            }

            if (!string.IsNullOrEmpty(p.SkyboxMaterial))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p.SkyboxMaterial);
                if (mat == null)
                    return ToolResult<LightingSetEnvironmentResult>.Fail(
                        $"Skybox material not found at '{p.SkyboxMaterial}'",
                        ErrorCodes.NOT_FOUND);
                RenderSettings.skybox = mat;
                changed++;
            }

            if (p.FogEnabled.HasValue)
            {
                RenderSettings.fog = p.FogEnabled.Value;
                changed++;
            }

            if (p.FogColor != null)
            {
                var color = LightingToolHelpers.ParseColor(p.FogColor);
                if (color.HasValue)
                {
                    RenderSettings.fogColor = color.Value;
                    changed++;
                }
            }

            if (p.FogDensity.HasValue)
            {
                RenderSettings.fogDensity = p.FogDensity.Value;
                changed++;
            }

            if (!string.IsNullOrEmpty(p.FogMode))
            {
                if (Enum.TryParse<FogMode>(p.FogMode, true, out var fogMode))
                {
                    RenderSettings.fogMode = fogMode;
                    changed++;
                }
                else
                {
                    return ToolResult<LightingSetEnvironmentResult>.Fail(
                        $"Invalid fog mode '{p.FogMode}'. Valid modes: Linear, Exponential, ExponentialSquared",
                        ErrorCodes.INVALID_PARAM);
                }
            }

            var ambientColor = RenderSettings.ambientLight;
            var fogColor = RenderSettings.fogColor;

            return ToolResult<LightingSetEnvironmentResult>.Ok(new LightingSetEnvironmentResult
            {
                AmbientMode      = RenderSettings.ambientMode.ToString(),
                AmbientColor     = new[] { ambientColor.r, ambientColor.g, ambientColor.b },
                AmbientIntensity = RenderSettings.ambientIntensity,
                FogEnabled       = RenderSettings.fog,
                FogColor         = new[] { fogColor.r, fogColor.g, fogColor.b },
                FogDensity       = RenderSettings.fogDensity,
                PropertiesChanged = changed
            });
        }
    }
}
