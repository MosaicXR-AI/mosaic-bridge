using UnityEditor;
using UnityEngine.Audio;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioMixerExposeParamTool
    {
        // O4 §4.4 (G4): AudioMixerController.exposedParameters is what backs "MusicVolume"/
        // "SFXVolume" sliders driven by AudioMixer.SetFloat at runtime. Its element type
        // (ExposedAudioParameter) is internal, so exposing/renaming/removing goes through
        // AudioMixerReflection; verify a newly exposed parameter with the PUBLIC AudioMixer.GetFloat.
        [MosaicTool("audio/mixer-expose-param",
                    "Exposes a group's Volume or Pitch as a named AudioMixer parameter (expose: GroupPath + " +
                    "ParamKind + Name), renames one (rename: Name + NewName), removes one (remove: Name), " +
                    "or lists all of them (list). Exposed names are what AudioMixer.SetFloat/GetFloat use " +
                    "at runtime.",
                    isReadOnly: false)]
        public static ToolResult<AudioMixerExposeParamResult> Execute(AudioMixerExposeParamParams p)
        {
            if (string.IsNullOrEmpty(p.MixerAssetPath))
                return ToolResult<AudioMixerExposeParamResult>.Fail("MixerAssetPath is required", ErrorCodes.INVALID_PARAM);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.MixerAssetPath);
            if (mixer == null)
                return ToolResult<AudioMixerExposeParamResult>.Fail(
                    $"AudioMixer not found at '{p.MixerAssetPath}'", ErrorCodes.NOT_FOUND);

            switch (p.Operation?.ToLowerInvariant())
            {
                case "expose": return Expose(mixer, p);
                case "rename": return Rename(mixer, p);
                case "remove": return Remove(mixer, p);
                case "list": return List(mixer, p);
                default:
                    return ToolResult<AudioMixerExposeParamResult>.Fail(
                        $"Invalid Operation '{p.Operation}'. Valid: expose, rename, remove, list", ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AudioMixerExposeParamResult> Expose(AudioMixer mixer, AudioMixerExposeParamParams p)
        {
            if (string.IsNullOrEmpty(p.Name))
                return ToolResult<AudioMixerExposeParamResult>.Fail("Name is required for expose", ErrorCodes.INVALID_PARAM);
            var kind = p.ParamKind?.Trim().ToLowerInvariant();
            if (kind != "volume" && kind != "pitch")
                return ToolResult<AudioMixerExposeParamResult>.Fail(
                    "ParamKind must be 'volume' or 'pitch'", ErrorCodes.INVALID_PARAM);
            if (!AudioToolHelpers.TryResolveMixerGroup(mixer, p.GroupPath, out var group, out var groupError))
                return ToolResult<AudioMixerExposeParamResult>.Fail(groupError, ErrorCodes.NOT_FOUND);

            if (!AudioMixerReflection.TryExposeParameter(mixer, group, kind, p.Name, out var error))
                return ToolResult<AudioMixerExposeParamResult>.Fail(error, ErrorCodes.INTERNAL_ERROR);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerExposeParamResult>.Ok(new AudioMixerExposeParamResult
            {
                Operation = "expose",
                MixerName = mixer.name,
                Message = $"Exposed {group.name}'s {kind} as '{p.Name}'. Verify with mixer.GetFloat(\"{p.Name}\", out _).",
            });
        }

        private static ToolResult<AudioMixerExposeParamResult> Rename(AudioMixer mixer, AudioMixerExposeParamParams p)
        {
            if (string.IsNullOrEmpty(p.Name) || string.IsNullOrEmpty(p.NewName))
                return ToolResult<AudioMixerExposeParamResult>.Fail(
                    "Name and NewName are required for rename", ErrorCodes.INVALID_PARAM);

            if (!AudioMixerReflection.TryRenameExposedParameter(mixer, p.Name, p.NewName, out var error))
                return ToolResult<AudioMixerExposeParamResult>.Fail(error, ErrorCodes.NOT_FOUND);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerExposeParamResult>.Ok(new AudioMixerExposeParamResult
            {
                Operation = "rename",
                MixerName = mixer.name,
                Message = $"Renamed exposed parameter '{p.Name}' to '{p.NewName}'.",
            });
        }

        private static ToolResult<AudioMixerExposeParamResult> Remove(AudioMixer mixer, AudioMixerExposeParamParams p)
        {
            if (string.IsNullOrEmpty(p.Name))
                return ToolResult<AudioMixerExposeParamResult>.Fail("Name is required for remove", ErrorCodes.INVALID_PARAM);

            if (!AudioMixerReflection.TryRemoveExposedParameter(mixer, p.Name, out var error))
                return ToolResult<AudioMixerExposeParamResult>.Fail(error, ErrorCodes.NOT_FOUND);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerExposeParamResult>.Ok(new AudioMixerExposeParamResult
            {
                Operation = "remove",
                MixerName = mixer.name,
                Message = $"Removed exposed parameter '{p.Name}'.",
            });
        }

        private static ToolResult<AudioMixerExposeParamResult> List(AudioMixer mixer, AudioMixerExposeParamParams p)
        {
            if (!AudioMixerReflection.TryListExposedParameters(mixer, out var names, out var error))
                return ToolResult<AudioMixerExposeParamResult>.Fail(error, ErrorCodes.INTERNAL_ERROR);

            return ToolResult<AudioMixerExposeParamResult>.Ok(new AudioMixerExposeParamResult
            {
                Operation = "list",
                MixerName = mixer.name,
                ExposedParameterNames = names,
                Message = $"{names.Length} exposed parameter(s).",
            });
        }
    }
}
