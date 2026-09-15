using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioCreateRandomContainerTool
    {
        [MosaicTool("audio/create-random-container",
                    "Creates an AudioRandomContainer asset (footstep/impact variation — Unity 6's " +
                    "randomized-playlist AudioResource) from a list of AudioClips, with optional " +
                    "PlaybackMode/TriggerMode/VolumeRandomizationRange. Assign the result to " +
                    "AudioSource via audio/set-source's Resource field.",
                    isReadOnly: false)]
        public static ToolResult<AudioCreateRandomContainerResult> Execute(AudioCreateRandomContainerParams p)
        {
            if (p.ClipPaths == null || p.ClipPaths.Length == 0)
                return ToolResult<AudioCreateRandomContainerResult>.Fail(
                    "ClipPaths requires at least one clip", ErrorCodes.INVALID_PARAM);

            if (AssetDatabase.LoadMainAssetAtPath(p.AssetPath) != null)
                return ToolResult<AudioCreateRandomContainerResult>.Fail(
                    $"An asset already exists at '{p.AssetPath}'", ErrorCodes.CONFLICT);

            if (!AudioRandomContainerReflection.TryCreate(
                    p.AssetPath, p.ClipPaths, p.ElementVolumes, p.PlaybackMode, p.TriggerMode,
                    p.VolumeRandomizationRange, out _, out string error))
            {
                return ToolResult<AudioCreateRandomContainerResult>.Fail(error, ErrorCodes.NOT_PERMITTED);
            }

            return ToolResult<AudioCreateRandomContainerResult>.Ok(new AudioCreateRandomContainerResult
            {
                AssetPath    = p.AssetPath,
                ElementCount = p.ClipPaths.Length,
                Message      = $"Created random container with {p.ClipPaths.Length} element(s) at '{p.AssetPath}'",
            });
        }
    }
}
