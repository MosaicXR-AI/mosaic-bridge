#if MOSAIC_HAS_HDRP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.HDRP
{
    public static class HdrpVolumeTool
    {
        [MosaicTool("hdrp/volume",
                    "Create and configure an HDRP Volume with a VolumeProfile and optional overrides (Exposure, Fog, SSAO, etc.)",
                    isReadOnly: false,
                    category: "hdrp")]
        public static ToolResult<HdrpVolumeResult> Execute(HdrpVolumeParams p)
        {
            var go = new GameObject(p.Name);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = p.IsGlobal;
            volume.priority = p.Priority;

            // Create a new VolumeProfile
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = p.Name + "_Profile";
            volume.profile = profile;

            var enabledOverrides = new List<string>();

            if (p.Overrides != null)
            {
                foreach (var kvp in p.Overrides)
                {
                    var component = AddOverrideByName(profile, kvp.Key);
                    if (component != null)
                    {
                        component.active = true;
                        enabledOverrides.Add(kvp.Key);

                        // Enable all parameters on this override so they take effect
                        foreach (var param in component.parameters)
                        {
                            param.overrideState = true;
                        }
                    }
                }
            }

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: HDRP Create Volume");

            return ToolResult<HdrpVolumeResult>.Ok(new HdrpVolumeResult
            {
                InstanceId = go.GetInstanceID(),
                Name = go.name,
                HierarchyPath = HdrpToolHelpers.GetHierarchyPath(go.transform),
                IsGlobal = volume.isGlobal,
                Priority = volume.priority,
                ProfileName = profile.name,
                EnabledOverrides = enabledOverrides.ToArray()
            });
        }

        private static VolumeComponent AddOverrideByName(VolumeProfile profile, string name)
        {
            switch (name?.ToLowerInvariant())
            {
                case "exposure":
                    return profile.Add<Exposure>(true);
                case "fog":
                    return profile.Add<Fog>(true);
                case "ssao":
                    return profile.Add<ScreenSpaceAmbientOcclusion>(true);
                case "bloom":
                    return profile.Add<Bloom>(true);
                case "coloradjustments":
                    return profile.Add<ColorAdjustments>(true);
                case "vignette":
                    return profile.Add<Vignette>(true);
                case "depthoffield":
                    return profile.Add<DepthOfField>(true);
                case "motionblur":
                    return profile.Add<MotionBlur>(true);
                case "chromaticaberration":
                    return profile.Add<ChromaticAberration>(true);
                case "tonemapping":
                    return profile.Add<Tonemapping>(true);
                case "whitebalance":
                    return profile.Add<WhiteBalance>(true);
                case "contactshadows":
                    return profile.Add<ContactShadows>(true);
                case "indirectlightingcontroller":
                    return profile.Add<IndirectLightingController>(true);
                default:
                    return null;
            }
        }
    }
}
#endif
