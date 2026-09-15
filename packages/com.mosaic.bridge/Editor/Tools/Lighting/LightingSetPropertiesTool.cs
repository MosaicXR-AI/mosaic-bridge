using System;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

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

            if (!string.IsNullOrEmpty(p.LightmapBakeType))
            {
                if (Enum.TryParse<LightmapBakeType>(p.LightmapBakeType, true, out var bakeType))
                {
                    light.lightmapBakeType = bakeType;
                    changed++;
                }
                else
                {
                    return ToolResult<LightingSetPropertiesResult>.Fail(
                        $"Invalid LightmapBakeType '{p.LightmapBakeType}'. Valid: Realtime, Mixed, Baked",
                        ErrorCodes.INVALID_PARAM);
                }
            }

            if (p.CookiePath != null)
            {
                if (p.CookiePath.Length == 0)
                {
                    light.cookie = null;
                }
                else
                {
                    var cookie = AssetDatabase.LoadAssetAtPath<Texture>(p.CookiePath);
                    if (cookie == null)
                        return ToolResult<LightingSetPropertiesResult>.Fail(
                            $"No Texture found at '{p.CookiePath}'", ErrorCodes.NOT_FOUND);
                    light.cookie = cookie;
                }
                changed++;
            }

            if (p.CookieSize != null)
            {
                if (p.CookieSize.Length != 2)
                    return ToolResult<LightingSetPropertiesResult>.Fail(
                        "CookieSize requires exactly [width, height]", ErrorCodes.INVALID_PARAM);
                light.cookieSize2D = new Vector2(p.CookieSize[0], p.CookieSize[1]);
                changed++;
            }

            if (!string.IsNullOrEmpty(p.CullingMask))
            {
                light.cullingMask = LayerMask.GetMask(
                    Array.ConvertAll(p.CullingMask.Split(','), s => s.Trim()));
                changed++;
            }

            if (p.ShadowBias.HasValue)
            {
                light.shadowBias = p.ShadowBias.Value;
                changed++;
            }

            if (p.AreaSize != null)
            {
                if (p.AreaSize.Length != 2)
                    return ToolResult<LightingSetPropertiesResult>.Fail(
                        "AreaSize requires exactly [width, height]", ErrorCodes.INVALID_PARAM);
                light.areaSize = new Vector2(p.AreaSize[0], p.AreaSize[1]);
                changed++;
            }

            return ToolResult<LightingSetPropertiesResult>.Ok(new LightingSetPropertiesResult
            {
                InstanceId        = UnityIds.Of(light.gameObject),
                Name              = light.gameObject.name,
                LightType         = light.type.ToString(),
                Intensity         = light.intensity,
                Shadows           = light.shadows.ToString(),
                PropertiesChanged = changed,
                LightmapBakeType  = light.lightmapBakeType.ToString(),
                CookiePath        = light.cookie != null ? AssetDatabase.GetAssetPath(light.cookie) : null,
                ShadowBias        = light.shadowBias,
            });
        }
    }
}
