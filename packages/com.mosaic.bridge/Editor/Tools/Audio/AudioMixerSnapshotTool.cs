using System.Linq;
using UnityEditor;
using UnityEngine.Audio;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioMixerSnapshotTool
    {
        // O4 §4.4 (G4): AudioMixerSnapshotController has a genuinely public constructor(AudioMixer)
        // and derives from the public AudioMixerSnapshot, so once created/resolved via
        // AudioMixerReflection, rename is plain Object.name — no reflection for that part.
        [MosaicTool("audio/mixer-snapshot",
                    "Adds a new snapshot (default values, like Unity's own 'Add Snapshot' — there is no " +
                    "clone-from-current-state API), renames one, sets which snapshot the Editor is currently " +
                    "editing (set-target), or lists all snapshots (list). Use audio/mixer-set-value to author " +
                    "a snapshot's per-group values and audio/mixer-transition to blend to it at runtime.",
                    isReadOnly: false)]
        public static ToolResult<AudioMixerSnapshotResult> Execute(AudioMixerSnapshotParams p)
        {
            if (string.IsNullOrEmpty(p.MixerAssetPath))
                return ToolResult<AudioMixerSnapshotResult>.Fail("MixerAssetPath is required", ErrorCodes.INVALID_PARAM);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.MixerAssetPath);
            if (mixer == null)
                return ToolResult<AudioMixerSnapshotResult>.Fail(
                    $"AudioMixer not found at '{p.MixerAssetPath}'", ErrorCodes.NOT_FOUND);

            switch (p.Operation?.ToLowerInvariant())
            {
                case "add": return Add(mixer, p);
                case "rename": return Rename(mixer, p);
                case "set-target": return SetTarget(mixer, p);
                case "list": return List(mixer, p);
                default:
                    return ToolResult<AudioMixerSnapshotResult>.Fail(
                        $"Invalid Operation '{p.Operation}'. Valid: add, rename, set-target, list", ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AudioMixerSnapshotResult> Add(AudioMixer mixer, AudioMixerSnapshotParams p)
        {
            if (string.IsNullOrEmpty(p.Name))
                return ToolResult<AudioMixerSnapshotResult>.Fail("Name is required for add", ErrorCodes.INVALID_PARAM);

            if (!AudioMixerReflection.TryCreateSnapshot(mixer, p.Name, out var snapshot, out var error))
                return ToolResult<AudioMixerSnapshotResult>.Fail(error, ErrorCodes.INTERNAL_ERROR);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerSnapshotResult>.Ok(new AudioMixerSnapshotResult
            {
                Operation = "add", MixerName = mixer.name, SnapshotName = snapshot.name,
                Message = $"Created snapshot '{snapshot.name}'.",
            });
        }

        private static ToolResult<AudioMixerSnapshotResult> Rename(AudioMixer mixer, AudioMixerSnapshotParams p)
        {
            if (string.IsNullOrEmpty(p.Name) || string.IsNullOrEmpty(p.NewName))
                return ToolResult<AudioMixerSnapshotResult>.Fail(
                    "Name and NewName are required for rename", ErrorCodes.INVALID_PARAM);

            if (!TryFindSnapshot(mixer, p.Name, out var snapshot, out var error))
                return ToolResult<AudioMixerSnapshotResult>.Fail(error, ErrorCodes.NOT_FOUND);

            Undo.RecordObject(snapshot, "Mosaic: Rename Mixer Snapshot");
            snapshot.name = p.NewName;
            AssetDatabase.SaveAssets();

            return ToolResult<AudioMixerSnapshotResult>.Ok(new AudioMixerSnapshotResult
            {
                Operation = "rename", MixerName = mixer.name, SnapshotName = snapshot.name,
                Message = $"Renamed snapshot '{p.Name}' to '{snapshot.name}'.",
            });
        }

        private static ToolResult<AudioMixerSnapshotResult> SetTarget(AudioMixer mixer, AudioMixerSnapshotParams p)
        {
            if (string.IsNullOrEmpty(p.Name))
                return ToolResult<AudioMixerSnapshotResult>.Fail("Name is required for set-target", ErrorCodes.INVALID_PARAM);
            if (!TryFindSnapshot(mixer, p.Name, out var snapshot, out var findError))
                return ToolResult<AudioMixerSnapshotResult>.Fail(findError, ErrorCodes.NOT_FOUND);

            if (!AudioMixerReflection.TrySetTargetSnapshot(mixer, snapshot, out var error))
                return ToolResult<AudioMixerSnapshotResult>.Fail(error, ErrorCodes.INTERNAL_ERROR);

            return ToolResult<AudioMixerSnapshotResult>.Ok(new AudioMixerSnapshotResult
            {
                Operation = "set-target", MixerName = mixer.name, SnapshotName = snapshot.name,
                Message = $"Editor is now editing snapshot '{snapshot.name}'.",
            });
        }

        private static ToolResult<AudioMixerSnapshotResult> List(AudioMixer mixer, AudioMixerSnapshotParams p)
        {
            if (!AudioMixerReflection.TryGetSnapshots(mixer, out var snapshots, out var error))
                return ToolResult<AudioMixerSnapshotResult>.Fail(error, ErrorCodes.INTERNAL_ERROR);

            return ToolResult<AudioMixerSnapshotResult>.Ok(new AudioMixerSnapshotResult
            {
                Operation = "list", MixerName = mixer.name,
                SnapshotNames = snapshots.Select(s => s.name).ToArray(),
                Message = $"{snapshots.Length} snapshot(s).",
            });
        }

        internal static bool TryFindSnapshot(AudioMixer mixer, string name, out AudioMixerSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (!AudioMixerReflection.TryGetSnapshots(mixer, out var snapshots, out error))
                return false;

            snapshot = snapshots.FirstOrDefault(s => s.name == name);
            if (snapshot == null)
            {
                error = $"No snapshot named '{name}' in mixer '{mixer.name}'.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
