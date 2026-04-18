using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Lighting
{
    public static class LightingInfoTool
    {
        [MosaicTool("lighting/info",
                    "Queries lighting state: individual light properties and environment settings",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<LightingInfoResult> Execute(LightingInfoParams p)
        {
            var lightInfos = new List<LightInfo>();

            // Specific light requested
            if (p.InstanceId != 0 || !string.IsNullOrEmpty(p.Name))
            {
                var light = LightingToolHelpers.FindLight(p.InstanceId, p.Name);
                if (light == null)
                    return ToolResult<LightingInfoResult>.Fail(
                        $"Light not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                        ErrorCodes.NOT_FOUND);

                lightInfos.Add(BuildLightInfo(light));
            }
            else
            {
                // All lights in scene
                var allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var light in allLights)
                    lightInfos.Add(BuildLightInfo(light));
            }

            var ambientColor = RenderSettings.ambientLight;
            var fogColor = RenderSettings.fogColor;
            var skybox = RenderSettings.skybox;

            return ToolResult<LightingInfoResult>.Ok(new LightingInfoResult
            {
                Lights = lightInfos.ToArray(),
                Environment = new EnvironmentInfo
                {
                    AmbientMode      = RenderSettings.ambientMode.ToString(),
                    AmbientColor     = new[] { ambientColor.r, ambientColor.g, ambientColor.b },
                    AmbientIntensity = RenderSettings.ambientIntensity,
                    SkyboxMaterial   = skybox != null ? UnityEditor.AssetDatabase.GetAssetPath(skybox) : null,
                    FogEnabled       = RenderSettings.fog,
                    FogColor         = new[] { fogColor.r, fogColor.g, fogColor.b },
                    FogDensity       = RenderSettings.fogDensity,
                    FogMode          = RenderSettings.fogMode.ToString()
                }
            });
        }

        private static LightInfo BuildLightInfo(Light light)
        {
            var c = light.color;
            return new LightInfo
            {
                InstanceId       = light.gameObject.GetInstanceID(),
                Name             = light.gameObject.name,
                HierarchyPath    = LightingToolHelpers.GetHierarchyPath(light.transform),
                LightType        = light.type.ToString(),
                Color            = new[] { c.r, c.g, c.b, c.a },
                Intensity        = light.intensity,
                Range            = light.range,
                SpotAngle        = light.spotAngle,
                Shadows          = light.shadows.ToString(),
                ColorTemperature = light.colorTemperature,
                BounceIntensity  = light.bounceIntensity,
                Enabled          = light.enabled
            };
        }
    }
}
