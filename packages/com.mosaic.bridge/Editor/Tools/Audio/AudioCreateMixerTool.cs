using System;
using System.IO;
using UnityEditor;
using UnityEngine.Audio;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioCreateMixerTool
    {
        // O4 §4.4 (G4): "AudioMixer : Object has an internal constructor and no public creation
        // path exists at all" — this reflects into UnityEditor.Audio.AudioMixerController, the
        // same class the Editor's own "Assets/Create/Audio/Audio Mixer" menu item uses (which
        // enters an interactive rename, making reflection the more predictable path here).
        [MosaicTool("audio/create-mixer",
                    "Creates an AudioMixer asset at AssetPath (must end in '.mixer'). Idempotent — " +
                    "returns the existing mixer if one is already there rather than erroring. Reflects into " +
                    "Unity's internal AudioMixerController (no public creation API exists); if that internal " +
                    "surface has moved on this Unity version, fails with a clear 'not reachable' message " +
                    "rather than a stack trace. Use audio/mixer-group to add groups, audio/route-source to " +
                    "wire an AudioSource to one.",
                    isReadOnly: false)]
        public static ToolResult<AudioCreateMixerResult> Execute(AudioCreateMixerParams p)
        {
            if (string.IsNullOrEmpty(p.AssetPath))
                return ToolResult<AudioCreateMixerResult>.Fail("AssetPath is required", ErrorCodes.INVALID_PARAM);
            if (!p.AssetPath.EndsWith(".mixer", StringComparison.OrdinalIgnoreCase))
                return ToolResult<AudioCreateMixerResult>.Fail("AssetPath must end with '.mixer'", ErrorCodes.INVALID_PARAM);

            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.AssetPath);
            if (existing != null)
                return ToolResult<AudioCreateMixerResult>.Ok(new AudioCreateMixerResult
                {
                    AssetPath = p.AssetPath,
                    MixerName = existing.name,
                    Created = false,
                    Message = $"An AudioMixer already exists at '{p.AssetPath}' — nothing created.",
                });

            var dir = Path.GetDirectoryName(p.AssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                return ToolResult<AudioCreateMixerResult>.Fail(
                    $"Directory '{dir}' does not exist. Create it first.", ErrorCodes.INVALID_PARAM);

            if (!AudioMixerReflection.TryCreateMixerAtPath(p.AssetPath, out var mixer, out var createError))
                return ToolResult<AudioCreateMixerResult>.Fail(createError, ErrorCodes.INTERNAL_ERROR);

            string masterName = null;
            if (AudioMixerReflection.TryGetMasterGroup(mixer, out var master, out _))
                masterName = master.name;

            AssetDatabase.SaveAssets();

            return ToolResult<AudioCreateMixerResult>.Ok(new AudioCreateMixerResult
            {
                AssetPath = p.AssetPath,
                MixerName = mixer.name,
                MasterGroupName = masterName,
                Created = true,
                Message = $"Created AudioMixer '{mixer.name}' at '{p.AssetPath}'.",
            });
        }
    }
}
