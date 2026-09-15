using UnityEditor;
using UnityEngine.Audio;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioMixerTransitionTool
    {
        // O4 §4.4 (G4): AudioMixerSnapshot.TransitionTo and AudioMixer.TransitionToSnapshots are
        // both fully public runtime APIs — no reflection needed here at all. Resolving a snapshot
        // BY NAME still needs AudioMixerReflection.TryGetSnapshots, since the only place snapshots
        // are enumerable (AudioMixerController.snapshots) is internal.
        [MosaicTool("audio/mixer-transition",
                    "Transitions (or blends) to one or more snapshots by name over TimeToReach seconds, e.g. " +
                    "the 'Paused'/'Underwater' lesson. Only observable in Play Mode — the transition target " +
                    "is set regardless, but nothing audible happens in edit mode.",
                    isReadOnly: false)]
        public static ToolResult<AudioMixerTransitionResult> Execute(AudioMixerTransitionParams p)
        {
            if (string.IsNullOrEmpty(p.MixerAssetPath))
                return ToolResult<AudioMixerTransitionResult>.Fail("MixerAssetPath is required", ErrorCodes.INVALID_PARAM);
            if (p.SnapshotNames == null || p.SnapshotNames.Length == 0)
                return ToolResult<AudioMixerTransitionResult>.Fail(
                    "SnapshotNames requires at least one entry", ErrorCodes.INVALID_PARAM);
            if (p.Weights != null && p.Weights.Length != p.SnapshotNames.Length)
                return ToolResult<AudioMixerTransitionResult>.Fail(
                    "Weights must have the same length as SnapshotNames", ErrorCodes.INVALID_PARAM);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.MixerAssetPath);
            if (mixer == null)
                return ToolResult<AudioMixerTransitionResult>.Fail(
                    $"AudioMixer not found at '{p.MixerAssetPath}'", ErrorCodes.NOT_FOUND);

            var snapshots = new AudioMixerSnapshot[p.SnapshotNames.Length];
            for (int i = 0; i < p.SnapshotNames.Length; i++)
            {
                if (!AudioMixerSnapshotTool.TryFindSnapshot(mixer, p.SnapshotNames[i], out var snapshot, out var error))
                    return ToolResult<AudioMixerTransitionResult>.Fail(error, ErrorCodes.NOT_FOUND);
                snapshots[i] = snapshot;
            }

            var weights = p.Weights ?? UniformWeights(p.SnapshotNames.Length);

            if (snapshots.Length == 1)
                snapshots[0].TransitionTo(p.TimeToReach);
            else
                mixer.TransitionToSnapshots(snapshots, weights, p.TimeToReach);

            string playModeNote = UnityEditor.EditorApplication.isPlaying
                ? ""
                : " (edit mode — nothing audible until Play Mode)";

            return ToolResult<AudioMixerTransitionResult>.Ok(new AudioMixerTransitionResult
            {
                MixerName = mixer.name,
                SnapshotNames = p.SnapshotNames,
                Weights = weights,
                TimeToReach = p.TimeToReach,
                Message = $"Transitioning to [{string.Join(", ", p.SnapshotNames)}] over {p.TimeToReach}s{playModeNote}",
            });
        }

        private static float[] UniformWeights(int count)
        {
            var weights = new float[count];
            for (int i = 0; i < count; i++) weights[i] = 1f / count;
            return weights;
        }
    }
}
