using UnityEditor;
using UnityEngine.Audio;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioMixerSetValueTool
    {
        // O4 §4.4 (G4): "duck music to -12dB in the Paused snapshot" needs a value stored ON that
        // snapshot, not the runtime AudioMixer.SetFloat (which is a live, unpersisted value). The
        // doc's cited AudioMixerGroupController.SetValueForVolume/Pitch does not exist; the real
        // API is AudioMixerSnapshotController.SetValue(GUID, float) directly on the snapshot,
        // confirmed via UnityCsReference source.
        [MosaicTool("audio/mixer-set-value",
                    "Sets (or reads) a group's Volume or Pitch value as authored INTO a specific snapshot " +
                    "(e.g. 'duck music to -12dB in the Paused snapshot') — distinct from AudioMixer.SetFloat, " +
                    "which only sets the current live value and does not persist to a snapshot asset.",
                    isReadOnly: false)]
        public static ToolResult<AudioMixerSetValueResult> Execute(AudioMixerSetValueParams p)
        {
            if (string.IsNullOrEmpty(p.MixerAssetPath))
                return ToolResult<AudioMixerSetValueResult>.Fail("MixerAssetPath is required", ErrorCodes.INVALID_PARAM);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.MixerAssetPath);
            if (mixer == null)
                return ToolResult<AudioMixerSetValueResult>.Fail(
                    $"AudioMixer not found at '{p.MixerAssetPath}'", ErrorCodes.NOT_FOUND);

            var kind = p.ParamKind?.Trim().ToLowerInvariant();
            if (kind != "volume" && kind != "pitch")
                return ToolResult<AudioMixerSetValueResult>.Fail(
                    "ParamKind must be 'volume' or 'pitch'", ErrorCodes.INVALID_PARAM);

            if (!AudioMixerSnapshotTool.TryFindSnapshot(mixer, p.SnapshotName, out var snapshot, out var snapshotError))
                return ToolResult<AudioMixerSetValueResult>.Fail(snapshotError, ErrorCodes.NOT_FOUND);

            if (!AudioToolHelpers.TryResolveMixerGroup(mixer, p.GroupPath, out var group, out var groupError))
                return ToolResult<AudioMixerSetValueResult>.Fail(groupError, ErrorCodes.NOT_FOUND);

            if (!AudioMixerReflection.TryGetParamGuid(group, kind, out var guid, out var guidError))
                return ToolResult<AudioMixerSetValueResult>.Fail(guidError, ErrorCodes.INTERNAL_ERROR);

            var operation = p.Operation?.ToLowerInvariant() ?? "set";
            if (operation == "get")
            {
                if (!AudioMixerReflection.TryGetSnapshotValue(snapshot, guid, out var readValue, out var readError))
                    return ToolResult<AudioMixerSetValueResult>.Fail(readError, ErrorCodes.NOT_FOUND);

                return ToolResult<AudioMixerSetValueResult>.Ok(new AudioMixerSetValueResult
                {
                    Operation = "get", MixerName = mixer.name, SnapshotName = snapshot.name,
                    GroupName = group.name, ParamKind = kind, Value = readValue,
                    Message = $"{group.name}.{kind} in snapshot '{snapshot.name}' = {readValue}",
                });
            }

            if (operation != "set")
                return ToolResult<AudioMixerSetValueResult>.Fail(
                    $"Invalid Operation '{p.Operation}'. Valid: set, get", ErrorCodes.INVALID_PARAM);

            if (!p.Value.HasValue)
                return ToolResult<AudioMixerSetValueResult>.Fail("Value is required for 'set'", ErrorCodes.INVALID_PARAM);

            if (!AudioMixerReflection.TrySetSnapshotValue(snapshot, guid, p.Value.Value, out var setError))
                return ToolResult<AudioMixerSetValueResult>.Fail(setError, ErrorCodes.INTERNAL_ERROR);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerSetValueResult>.Ok(new AudioMixerSetValueResult
            {
                Operation = "set", MixerName = mixer.name, SnapshotName = snapshot.name,
                GroupName = group.name, ParamKind = kind, Value = p.Value.Value,
                Message = $"Set {group.name}.{kind} = {p.Value.Value} in snapshot '{snapshot.name}'.",
            });
        }
    }
}
