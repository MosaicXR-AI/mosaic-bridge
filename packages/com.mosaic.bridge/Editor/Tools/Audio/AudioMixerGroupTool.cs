using UnityEditor;
using UnityEngine.Audio;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioMixerGroupTool
    {
        // O4 §4.4 (G4): rename uses the plain public UnityEngine.Object.name — no reflection
        // needed for that one. add/delete/move go through AudioMixerReflection since
        // AudioMixerController.CreateNewGroup/AddChildToParent/DeleteGroups are internal.
        [MosaicTool("audio/mixer-group",
                    "Adds, renames, deletes, or moves a group in an existing AudioMixer asset. GroupPath/" +
                    "ParentPath are matched via AudioMixer.FindMatchingGroups — a bare name ('SFX') or a " +
                    "sub-path ('Master/SFX') to disambiguate. add: Name (+ optional ParentPath, default " +
                    "master group). rename: GroupPath + Name. delete: GroupPath. move: GroupPath + ParentPath. " +
                    "Reflects into Unity's internal AudioMixerController for add/delete/move; fails with a " +
                    "clear 'not reachable' message rather than a stack trace if that internal surface has moved.",
                    isReadOnly: false)]
        public static ToolResult<AudioMixerGroupResult> Execute(AudioMixerGroupParams p)
        {
            if (string.IsNullOrEmpty(p.MixerAssetPath))
                return ToolResult<AudioMixerGroupResult>.Fail("MixerAssetPath is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.Operation))
                return ToolResult<AudioMixerGroupResult>.Fail(
                    "Operation is required. Valid: add, rename, delete, move", ErrorCodes.INVALID_PARAM);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(p.MixerAssetPath);
            if (mixer == null)
                return ToolResult<AudioMixerGroupResult>.Fail(
                    $"AudioMixer not found at '{p.MixerAssetPath}'", ErrorCodes.NOT_FOUND);

            var operation = p.Operation.ToLowerInvariant();
            switch (operation)
            {
                case "add": return Add(mixer, p);
                case "rename": return Rename(mixer, p);
                case "delete": return Delete(mixer, p);
                case "move": return Move(mixer, p);
                default:
                    return ToolResult<AudioMixerGroupResult>.Fail(
                        $"Invalid Operation '{p.Operation}'. Valid: add, rename, delete, move", ErrorCodes.INVALID_PARAM);
            }
        }

        private static bool TryResolveParent(AudioMixer mixer, string parentPath, out AudioMixerGroup parent, out string error)
        {
            if (string.IsNullOrEmpty(parentPath))
                return AudioMixerReflection.TryGetMasterGroup(mixer, out parent, out error);
            return AudioToolHelpers.TryResolveMixerGroup(mixer, parentPath, out parent, out error);
        }

        private static ToolResult<AudioMixerGroupResult> Add(AudioMixer mixer, AudioMixerGroupParams p)
        {
            if (string.IsNullOrEmpty(p.Name))
                return ToolResult<AudioMixerGroupResult>.Fail("Name is required for add", ErrorCodes.INVALID_PARAM);

            if (!TryResolveParent(mixer, p.ParentPath, out var parent, out var parentError))
                return ToolResult<AudioMixerGroupResult>.Fail(parentError, ErrorCodes.NOT_FOUND);

            if (!AudioMixerReflection.TryCreateNewGroup(mixer, p.Name, out var group, out var createError))
                return ToolResult<AudioMixerGroupResult>.Fail(createError, ErrorCodes.INTERNAL_ERROR);

            if (!AudioMixerReflection.TryAddChildToParent(mixer, group, parent, out var attachError))
                return ToolResult<AudioMixerGroupResult>.Fail(
                    $"Group '{group.name}' was created but could not be attached under '{parent.name}': {attachError}",
                    ErrorCodes.INTERNAL_ERROR);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerGroupResult>.Ok(new AudioMixerGroupResult
            {
                Operation = "add",
                MixerName = mixer.name,
                GroupName = group.name,
                ParentName = parent.name,
                Message = $"Created group '{group.name}' under '{parent.name}'.",
            });
        }

        private static ToolResult<AudioMixerGroupResult> Rename(AudioMixer mixer, AudioMixerGroupParams p)
        {
            if (string.IsNullOrEmpty(p.Name))
                return ToolResult<AudioMixerGroupResult>.Fail("Name is required for rename", ErrorCodes.INVALID_PARAM);
            if (!AudioToolHelpers.TryResolveMixerGroup(mixer, p.GroupPath, out var group, out var error))
                return ToolResult<AudioMixerGroupResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var oldName = group.name;
            Undo.RecordObject(group, "Mosaic: Rename Mixer Group");
            group.name = p.Name;
            AssetDatabase.SaveAssets();

            return ToolResult<AudioMixerGroupResult>.Ok(new AudioMixerGroupResult
            {
                Operation = "rename",
                MixerName = mixer.name,
                GroupName = group.name,
                Message = $"Renamed group '{oldName}' to '{group.name}'.",
            });
        }

        private static ToolResult<AudioMixerGroupResult> Delete(AudioMixer mixer, AudioMixerGroupParams p)
        {
            if (!AudioToolHelpers.TryResolveMixerGroup(mixer, p.GroupPath, out var group, out var error))
                return ToolResult<AudioMixerGroupResult>.Fail(error, ErrorCodes.NOT_FOUND);

            if (AudioMixerReflection.TryGetMasterGroup(mixer, out var master, out _) && group == master)
                return ToolResult<AudioMixerGroupResult>.Fail(
                    "Cannot delete the mixer's master group.", ErrorCodes.INVALID_PARAM);

            var name = group.name;
            if (!AudioMixerReflection.TryDeleteGroups(mixer, new[] { group }, out var deleteError))
                return ToolResult<AudioMixerGroupResult>.Fail(deleteError, ErrorCodes.INTERNAL_ERROR);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerGroupResult>.Ok(new AudioMixerGroupResult
            {
                Operation = "delete",
                MixerName = mixer.name,
                GroupName = name,
                Message = $"Deleted group '{name}'.",
            });
        }

        private static ToolResult<AudioMixerGroupResult> Move(AudioMixer mixer, AudioMixerGroupParams p)
        {
            if (string.IsNullOrEmpty(p.ParentPath))
                return ToolResult<AudioMixerGroupResult>.Fail("ParentPath is required for move", ErrorCodes.INVALID_PARAM);
            if (!AudioToolHelpers.TryResolveMixerGroup(mixer, p.GroupPath, out var group, out var groupError))
                return ToolResult<AudioMixerGroupResult>.Fail(groupError, ErrorCodes.NOT_FOUND);
            if (!AudioToolHelpers.TryResolveMixerGroup(mixer, p.ParentPath, out var newParent, out var parentError))
                return ToolResult<AudioMixerGroupResult>.Fail(parentError, ErrorCodes.NOT_FOUND);
            if (group == newParent)
                return ToolResult<AudioMixerGroupResult>.Fail("A group cannot be its own parent.", ErrorCodes.INVALID_PARAM);

            if (!AudioMixerReflection.TryAddChildToParent(mixer, group, newParent, out var moveError))
                return ToolResult<AudioMixerGroupResult>.Fail(moveError, ErrorCodes.INTERNAL_ERROR);

            AssetDatabase.SaveAssets();
            return ToolResult<AudioMixerGroupResult>.Ok(new AudioMixerGroupResult
            {
                Operation = "move",
                MixerName = mixer.name,
                GroupName = group.name,
                ParentName = newParent.name,
                Message = $"Moved group '{group.name}' under '{newParent.name}'. Verify with audio/mixer-info " +
                          "— AddChildToParent's re-parenting behavior when a group already has a parent has not " +
                          "been runtime-verified in this environment.",
            });
        }
    }
}
