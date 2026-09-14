using UnityEngine.Audio;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioRouteSourceTool
    {
        // O4 §4.4 (G4/D5): "mixers had to be wired by hand-written editor scripts" was the
        // most-hit audio gap in the field report — AudioMixer.FindMatchingGroups is fully public,
        // so this needed no reflection at all, unlike mixer creation/group authoring.
        [MosaicTool("audio/route-source",
                    "Routes an AudioSource to a group in an existing AudioMixer asset. GroupPath is matched via " +
                    "AudioMixer.FindMatchingGroups — a bare name ('SFX') or a sub-path ('Master/SFX') to " +
                    "disambiguate when more than one group shares a name.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AudioRouteSourceResult> Execute(AudioRouteSourceParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<AudioRouteSourceResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            var source = AudioToolHelpers.ResolveAudioSource(p.InstanceId, p.Name, out var go);
            if (go == null)
                return ToolResult<AudioRouteSourceResult>.Fail(
                    $"GameObject not found (InstanceId={p.InstanceId}, Name='{p.Name}')", ErrorCodes.NOT_FOUND);
            if (source == null)
                return ToolResult<AudioRouteSourceResult>.Fail(
                    $"No AudioSource found on GameObject '{go.name}'", ErrorCodes.NOT_FOUND);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.MixerAssetPath);
            if (mixer == null)
                return ToolResult<AudioRouteSourceResult>.Fail(
                    $"AudioMixer not found at '{p.MixerAssetPath}'", ErrorCodes.NOT_FOUND);

            if (!AudioToolHelpers.TryResolveMixerGroup(mixer, p.GroupPath, out var group, out var groupError))
                return ToolResult<AudioRouteSourceResult>.Fail(groupError, ErrorCodes.NOT_FOUND);

            Undo.RecordObject(source, "Mosaic: Route Audio Source");
            source.outputAudioMixerGroup = group;

            return ToolResult<AudioRouteSourceResult>.Ok(new AudioRouteSourceResult
            {
                InstanceId = UnityIds.Of(go),
                GameObjectName = go.name,
                MixerName = mixer.name,
                GroupName = group.name,
            });
        }
    }
}
