using System;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Lighting
{
    public static class LightingSetPropertiesTool
    {
        [MosaicTool("lighting/set-properties",
                    "Modifies properties of an existing Light component",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<LightingSetPropertiesResult> Execute(LightingSetPropertiesParams p)
        {
            if (p.InstanceId == 0 && string.IsNullOrEmpty(p.Name))
                return ToolResult<LightingSetPropertiesResult>.Fail(
                    "Either InstanceId or Name must be provided",
                    ErrorCodes.INVALID_PARAM);

            var light = LightingToolHelpers.FindLight(p.InstanceId, p.Name);
            if (light == null)
                return ToolResult<LightingSetPropertiesResult>.Fail(
                    $"Light not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(light, "Mosaic: Set Light Properties");

            int changed = 0;

            if (p.Color != null)
            {
                var color = LightingToolHelpers.ParseColor(p.Color);
                if (color.HasValue) { light.color = color.Value; changed++; }
            }

            if (p.Intensity.HasValue)
            {
                light.intensity = p.Intensity.Value;
                changed++;
            }

            if (p.Range.HasValue)
            {
                light.range = p.Range.Value;
                changed++;
            }

            if (p.SpotAngle.HasValue)
            {
                light.spotAngle = p.SpotAngle.Value;
                changed++;
            }

            if (!string.IsNullOrEmpty(p.Shadows))
            {
                if (Enum.TryParse<LightShadows>(p.Shadows, true, out var shadows))
                {
                    light.shadows = shadows;
                    changed++;
                }
                else
                {
                    return ToolResult<LightingSetPropertiesResult>.Fail(
                        $"Invalid shadow type '{p.Shadows}'. Valid types: None, Hard, Soft",
                        ErrorCodes.INVALID_PARAM);
                }
            }

            if (p.ColorTemperature.HasValue)
            {
                light.colorTemperature = p.ColorTemperature.Value;
                changed++;
            }

            if (p.BounceIntensity.HasValue)
            {
                light.bounceIntensity = p.BounceIntensity.Value;
                changed++;
            }

            return ToolResult<LightingSetPropertiesResult>.Ok(new LightingSetPropertiesResult
            {
                InstanceId        = light.gameObject.GetInstanceID(),
                Name              = light.gameObject.name,
                LightType         = light.type.ToString(),
                Intensity         = light.intensity,
                Shadows           = light.shadows.ToString(),
                PropertiesChanged = changed
            });
        }
    }
}
