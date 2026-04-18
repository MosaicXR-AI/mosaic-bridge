#if MOSAIC_HAS_URP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.URP
{
    public static class UrpVolumeTool
    {
        [MosaicTool("urp/volume",
                    "Create and configure a URP post-processing Volume with a VolumeProfile and optional overrides",
                    isReadOnly: false,
                    category: "urp")]
        public static ToolResult<UrpVolumeResult> Execute(UrpVolumeParams p)
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

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: URP Create Volume");

            return ToolResult<UrpVolumeResult>.Ok(new UrpVolumeResult
            {
                InstanceId = go.GetInstanceID(),
                Name = go.name,
                HierarchyPath = UrpToolHelpers.GetHierarchyPath(go.transform),
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
                case "bloom":
                    return profile.Add<Bloom>(true);
                case "coloradjustments":
                    return profile.Add<ColorAdjustments>(true);
                case "vignette":
                    return profile.Add<Vignette>(true);
                case "channelmixer":
                    return profile.Add<ChannelMixer>(true);
                case "chromaticaberration":
                    return profile.Add<ChromaticAberration>(true);
                case "depthoffield":
                    return profile.Add<DepthOfField>(true);
                case "filmgrain":
                    return profile.Add<FilmGrain>(true);
                case "lensdistortion":
                    return profile.Add<LensDistortion>(true);
                case "motionblur":
                    return profile.Add<MotionBlur>(true);
                case "tonemapping":
                    return profile.Add<Tonemapping>(true);
                case "whitebalance":
                    return profile.Add<WhiteBalance>(true);
                default:
                    return null;
            }
        }
    }
}
#endif
