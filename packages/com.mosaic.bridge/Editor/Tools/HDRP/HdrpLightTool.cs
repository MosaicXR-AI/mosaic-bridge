#if MOSAIC_HAS_HDRP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.HDRP
{
    public static class HdrpLightTool
    {
        [MosaicTool("hdrp/light",
                    "Configure HDRP-specific light settings including area light shapes, volumetric contribution, and shadow resolution",
                    isReadOnly: false,
                    category: "hdrp")]
        public static ToolResult<HdrpLightResult> Execute(HdrpLightParams p)
        {
            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<HdrpLightResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found.",
                    ErrorCodes.NOT_FOUND);

            var light = go.GetComponent<Light>();
            if (light == null)
                return ToolResult<HdrpLightResult>.Fail(
                    $"GameObject '{p.GameObjectName}' does not have a Light component.",
                    ErrorCodes.NOT_FOUND);

            var hdLight = go.GetComponent<HDAdditionalLightData>();
            if (hdLight == null)
                hdLight = go.AddComponent<HDAdditionalLightData>();

            Undo.RecordObject(light, "Mosaic: HDRP Configure Light");
            Undo.RecordObject(hdLight, "Mosaic: HDRP Configure Light");

            // Area light shape
            string areaShape = null;
            if (!string.IsNullOrEmpty(p.AreaLightShape))
            {
                // Unity 2023.2+ folded the area-light shapes into LightType itself:
                // LightType.Area plus HDAdditionalLightData.SetAreaLightShape became
                // LightType.Rectangle/Disc/Tube. The old pair is obsolete-as-error on 6.5
                // and SetAreaLightShape no longer exists at all.
                switch (p.AreaLightShape.ToLowerInvariant())
                {
                    case "rectangle":
                        light.type = LightType.Rectangle;
                        areaShape = "Rectangle";
                        break;
                    case "disc":
                        light.type = LightType.Disc;
                        areaShape = "Disc";
                        break;
                    case "tube":
                        light.type = LightType.Tube;
                        areaShape = "Tube";
                        break;
                    default:
                        return ToolResult<HdrpLightResult>.Fail(
                            $"Invalid AreaLightShape '{p.AreaLightShape}'. Valid: Rectangle, Disc, Tube.",
                            ErrorCodes.INVALID_PARAM);
                }
            }

            // Intensity. HDAdditionalLightData.SetIntensity is deprecated in favour of
            // converting explicitly and assigning Light.intensity. Reproduced exactly as
            // SetIntensity did it internally — interpret the value in the light's configured
            // unit, convert to the type's native unit — so behaviour is unchanged.
            if (p.Intensity.HasValue)
            {
                light.intensity = LightUnitUtils.ConvertIntensity(
                    light,
                    p.Intensity.Value,
                    light.lightUnit,
                    LightUnitUtils.GetNativeLightUnit(light.type));
            }

            // Color temperature
            if (p.ColorTemperature.HasValue)
            {
                light.useColorTemperature = true;
                light.colorTemperature = p.ColorTemperature.Value;
            }

            // Volumetric dimmer
            if (p.VolumetricDimmer.HasValue)
                hdLight.volumetricDimmer = p.VolumetricDimmer.Value;

            // Shadow resolution
            if (p.ShadowResolution.HasValue)
            {
                hdLight.SetShadowResolution(p.ShadowResolution.Value);
            }

            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(hdLight);

            return ToolResult<HdrpLightResult>.Ok(new HdrpLightResult
            {
                InstanceId = UnityIds.Of(go),
                Name = go.name,
                HierarchyPath = HdrpToolHelpers.GetHierarchyPath(go.transform),
                LightType = light.type.ToString(),
                AreaLightShape = areaShape ?? "N/A",
                Intensity = light.intensity,
                ColorTemperature = light.colorTemperature,
                VolumetricDimmer = hdLight.volumetricDimmer,
                ShadowResolution = 0 // Resolution is stored internally
            });
        }
    }
}
#endif
