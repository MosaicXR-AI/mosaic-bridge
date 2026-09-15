using System.Linq;
using UnityEditor;
using UnityEngine.Audio;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioMixerEffectTool
    {
        // O4 §4.4 (G4) — the doc's own "build last" item: effect names are native registry
        // strings (never hardcode), and AudioMixerGroupController.effects turned out to be a
        // public get/set array (the doc's cited InsertEffect does not exist), matching the same
        // array-reassign pattern as exposedParameters/snapshots. Sound design: sidechain ducking,
        // reverb send bus, lowpass "muffled when paused".
        [MosaicTool("audio/mixer-effect",
                    "Adds/removes an effect on a group's effect chain, sets/gets its Mix Level or a named " +
                    "parameter's value AS AUTHORED INTO a snapshot, routes one effect's sidechain send to " +
                    "another (send-to), or lists the group's current effects / every valid EffectType name " +
                    "(list-types — always check this before 'add', names are a native registry that varies " +
                    "by Unity install).",
                    isReadOnly: false)]
        public static ToolResult<AudioMixerEffectResult> Execute(AudioMixerEffectParams p)
        {
            if (string.IsNullOrEmpty(p.MixerAssetPath))
                return ToolResult<AudioMixerEffectResult>.Fail("MixerAssetPath is required", ErrorCodes.INVALID_PARAM);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.MixerAssetPath);
            if (mixer == null)
                return ToolResult<AudioMixerEffectResult>.Fail(
                    $"AudioMixer not found at '{p.MixerAssetPath}'", ErrorCodes.NOT_FOUND);

            var operation = p.Operation?.ToLowerInvariant();

            if (operation == "list-types")
            {
                if (!AudioMixerReflection.TryGetAudioEffectNames(out var names, out var typesError))
                    return ToolResult<AudioMixerEffectResult>.Fail(typesError, ErrorCodes.INTERNAL_ERROR);
                return ToolResult<AudioMixerEffectResult>.Ok(new AudioMixerEffectResult
                {
                    Operation = "list-types", MixerName = mixer.name,
                    AvailableEffectTypes = names, Message = $"{names.Length} available effect type(s).",
                });
            }

            if (string.IsNullOrEmpty(p.GroupPath))
                return ToolResult<AudioMixerEffectResult>.Fail("GroupPath is required", ErrorCodes.INVALID_PARAM);
            if (!AudioToolHelpers.TryResolveMixerGroup(mixer, p.GroupPath, out var group, out var groupError))
                return ToolResult<AudioMixerEffectResult>.Fail(groupError, ErrorCodes.NOT_FOUND);

            switch (operation)
            {
                case "add": return Add(mixer, group, p);
                case "remove": return Remove(mixer, group, p);
                case "set-value": return SetValue(mixer, group, p);
                case "get-value": return GetValue(mixer, group, p);
                case "send-to": return SendTo(mixer, group, p);
                case "list": return List(mixer, group, p);
                default:
                    return ToolResult<AudioMixerEffectResult>.Fail(
                        $"Invalid Operation '{p.Operation}'. Valid: add, remove, set-value, get-value, send-to, list, list-types",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AudioMixerEffectResult> Add(AudioMixer mixer, AudioMixerGroup group, AudioMixerEffectParams p)
        {
            if (string.IsNullOrEmpty(p.EffectType))
                return ToolResult<AudioMixerEffectResult>.Fail("EffectType is required for add", ErrorCodes.INVALID_PARAM);

            if (!AudioMixerReflection.TryGetAudioEffectNames(out var validTypes, out var typesError))
                return ToolResult<AudioMixerEffectResult>.Fail(typesError, ErrorCodes.INTERNAL_ERROR);
            if (!validTypes.Contains(p.EffectType))
                return ToolResult<AudioMixerEffectResult>.Fail(
                    $"Unknown EffectType '{p.EffectType}'. Valid: {string.Join(", ", validTypes)}", ErrorCodes.INVALID_PARAM);

            if (!AudioMixerReflection.TryAddEffect(mixer, group, p.EffectType, out var index, out var error))
                return ToolResult<AudioMixerEffectResult>.Fail(error, ErrorCodes.INTERNAL_ERROR);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerEffectResult>.Ok(new AudioMixerEffectResult
            {
                Operation = "add", MixerName = mixer.name, GroupName = group.name,
                EffectIndex = index, EffectType = p.EffectType,
                Message = $"Added '{p.EffectType}' to '{group.name}' at index {index}.",
            });
        }

        private static ToolResult<AudioMixerEffectResult> Remove(AudioMixer mixer, AudioMixerGroup group, AudioMixerEffectParams p)
        {
            if (!p.EffectIndex.HasValue)
                return ToolResult<AudioMixerEffectResult>.Fail("EffectIndex is required for remove", ErrorCodes.INVALID_PARAM);

            if (!AudioMixerReflection.TryRemoveEffect(group, p.EffectIndex.Value, out var error))
                return ToolResult<AudioMixerEffectResult>.Fail(error, ErrorCodes.OUT_OF_RANGE);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerEffectResult>.Ok(new AudioMixerEffectResult
            {
                Operation = "remove", MixerName = mixer.name, GroupName = group.name, EffectIndex = p.EffectIndex.Value,
                Message = $"Removed effect at index {p.EffectIndex.Value} from '{group.name}'.",
            });
        }

        private static ToolResult<AudioMixerEffectResult> SetValue(AudioMixer mixer, AudioMixerGroup group, AudioMixerEffectParams p)
        {
            if (!p.EffectIndex.HasValue)
                return ToolResult<AudioMixerEffectResult>.Fail("EffectIndex is required for set-value", ErrorCodes.INVALID_PARAM);
            if (!p.Value.HasValue)
                return ToolResult<AudioMixerEffectResult>.Fail("Value is required for set-value", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.SnapshotName))
                return ToolResult<AudioMixerEffectResult>.Fail("SnapshotName is required for set-value", ErrorCodes.INVALID_PARAM);
            if (!AudioMixerSnapshotTool.TryFindSnapshot(mixer, p.SnapshotName, out var snapshot, out var snapshotError))
                return ToolResult<AudioMixerEffectResult>.Fail(snapshotError, ErrorCodes.NOT_FOUND);

            if (!AudioMixerReflection.TrySetEffectValue(
                    mixer, group, p.EffectIndex.Value, snapshot, p.ParamName, p.Value.Value, out var error))
                return ToolResult<AudioMixerEffectResult>.Fail(error, ErrorCodes.INTERNAL_ERROR);

            AssetDatabase.SaveAssets();
            var paramLabel = string.IsNullOrEmpty(p.ParamName) ? "Mix Level" : p.ParamName;
            return ToolResult<AudioMixerEffectResult>.Ok(new AudioMixerEffectResult
            {
                Operation = "set-value", MixerName = mixer.name, GroupName = group.name,
                EffectIndex = p.EffectIndex.Value, Value = p.Value.Value,
                Message = $"Set effect[{p.EffectIndex.Value}].{paramLabel} = {p.Value.Value} in snapshot '{snapshot.name}'.",
            });
        }

        private static ToolResult<AudioMixerEffectResult> GetValue(AudioMixer mixer, AudioMixerGroup group, AudioMixerEffectParams p)
        {
            if (!p.EffectIndex.HasValue)
                return ToolResult<AudioMixerEffectResult>.Fail("EffectIndex is required for get-value", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.SnapshotName))
                return ToolResult<AudioMixerEffectResult>.Fail("SnapshotName is required for get-value", ErrorCodes.INVALID_PARAM);
            if (!AudioMixerSnapshotTool.TryFindSnapshot(mixer, p.SnapshotName, out var snapshot, out var snapshotError))
                return ToolResult<AudioMixerEffectResult>.Fail(snapshotError, ErrorCodes.NOT_FOUND);

            if (!AudioMixerReflection.TryGetEffectValue(
                    mixer, group, p.EffectIndex.Value, snapshot, p.ParamName, out var value, out var error))
                return ToolResult<AudioMixerEffectResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var paramLabel = string.IsNullOrEmpty(p.ParamName) ? "Mix Level" : p.ParamName;
            return ToolResult<AudioMixerEffectResult>.Ok(new AudioMixerEffectResult
            {
                Operation = "get-value", MixerName = mixer.name, GroupName = group.name,
                EffectIndex = p.EffectIndex.Value, Value = value,
                Message = $"effect[{p.EffectIndex.Value}].{paramLabel} in snapshot '{snapshot.name}' = {value}",
            });
        }

        private static ToolResult<AudioMixerEffectResult> SendTo(AudioMixer mixer, AudioMixerGroup group, AudioMixerEffectParams p)
        {
            if (!p.EffectIndex.HasValue)
                return ToolResult<AudioMixerEffectResult>.Fail("EffectIndex is required for send-to", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.SendTargetGroupPath) || !p.SendTargetEffectIndex.HasValue)
                return ToolResult<AudioMixerEffectResult>.Fail(
                    "SendTargetGroupPath and SendTargetEffectIndex are required for send-to", ErrorCodes.INVALID_PARAM);
            if (!AudioToolHelpers.TryResolveMixerGroup(mixer, p.SendTargetGroupPath, out var targetGroup, out var targetError))
                return ToolResult<AudioMixerEffectResult>.Fail(targetError, ErrorCodes.NOT_FOUND);

            if (!AudioMixerReflection.TrySetSendTarget(
                    group, p.EffectIndex.Value, targetGroup, p.SendTargetEffectIndex.Value, out var error))
                return ToolResult<AudioMixerEffectResult>.Fail(error, ErrorCodes.NOT_PERMITTED);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerEffectResult>.Ok(new AudioMixerEffectResult
            {
                Operation = "send-to", MixerName = mixer.name, GroupName = group.name, EffectIndex = p.EffectIndex.Value,
                Message = $"'{group.name}' effect[{p.EffectIndex.Value}] now sends to " +
                          $"'{targetGroup.name}' effect[{p.SendTargetEffectIndex.Value}].",
            });
        }

        private static ToolResult<AudioMixerEffectResult> List(AudioMixer mixer, AudioMixerGroup group, AudioMixerEffectParams p)
        {
            if (!AudioMixerReflection.TryGetEffects(group, out var effects, out var error))
                return ToolResult<AudioMixerEffectResult>.Fail(error, ErrorCodes.INTERNAL_ERROR);

            return ToolResult<AudioMixerEffectResult>.Ok(new AudioMixerEffectResult
            {
                Operation = "list", MixerName = mixer.name, GroupName = group.name,
                EffectNames = effects.Select(e => e.name).ToArray(),
                Message = $"{effects.Length} effect(s) on '{group.name}'.",
            });
        }
    }
}
