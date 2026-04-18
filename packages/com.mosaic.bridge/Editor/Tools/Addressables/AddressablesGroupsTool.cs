#if MOSAIC_HAS_ADDRESSABLES
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Addressables
{
    public static class AddressablesGroupsTool
    {
        [MosaicTool("addressables/groups",
                    "Manages addressable groups: list all groups, create a new group, or delete an existing group",
                    isReadOnly: false)]
        public static ToolResult<AddressablesGroupsResult> Groups(AddressablesGroupsParams p)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return ToolResult<AddressablesGroupsResult>.Fail(
                    "Addressable Asset Settings not found. Ensure the Addressables package is configured.",
                    ErrorCodes.NOT_FOUND,
                    "Open Window > Asset Management > Addressables > Groups to initialize settings.");

            switch (p.Action?.ToLowerInvariant())
            {
                case "list":
                    return ListGroups(settings);
                case "create":
                    return CreateGroup(settings, p.GroupName);
                case "delete":
                    return DeleteGroup(settings, p.GroupName, p.MoveEntriesToDefault);
                default:
                    return ToolResult<AddressablesGroupsResult>.Fail(
                        $"Invalid action '{p.Action}'. Valid actions: list, create, delete.",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AddressablesGroupsResult> ListGroups(AddressableAssetSettings settings)
        {
            var groups = settings.groups
                .Where(g => g != null)
                .Select(g => new GroupInfo
                {
                    Name = g.Name,
                    EntryCount = g.entries.Count,
                    IsDefault = g == settings.DefaultGroup,
                    IsReadOnly = g.ReadOnly
                })
                .ToArray();

            return ToolResult<AddressablesGroupsResult>.Ok(new AddressablesGroupsResult
            {
                Action = "list",
                Groups = groups,
                Message = $"Found {groups.Length} addressable group(s)."
            });
        }

        private static ToolResult<AddressablesGroupsResult> CreateGroup(AddressableAssetSettings settings, string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return ToolResult<AddressablesGroupsResult>.Fail(
                    "GroupName is required for 'create' action.",
                    ErrorCodes.INVALID_PARAM);

            if (settings.FindGroup(groupName) != null)
                return ToolResult<AddressablesGroupsResult>.Fail(
                    $"Group '{groupName}' already exists.",
                    ErrorCodes.CONFLICT);

            var group = settings.CreateGroup(groupName, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupAdded, group, true);

            return ToolResult<AddressablesGroupsResult>.Ok(new AddressablesGroupsResult
            {
                Action = "create",
                Groups = new[]
                {
                    new GroupInfo
                    {
                        Name = group.Name,
                        EntryCount = 0,
                        IsDefault = false,
                        IsReadOnly = false
                    }
                },
                Message = $"Created group '{groupName}'."
            });
        }

        private static ToolResult<AddressablesGroupsResult> DeleteGroup(AddressableAssetSettings settings, string groupName, bool moveEntries)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return ToolResult<AddressablesGroupsResult>.Fail(
                    "GroupName is required for 'delete' action.",
                    ErrorCodes.INVALID_PARAM);

            var group = settings.FindGroup(groupName);
            if (group == null)
                return ToolResult<AddressablesGroupsResult>.Fail(
                    $"Group '{groupName}' not found.",
                    ErrorCodes.NOT_FOUND);

            if (group == settings.DefaultGroup)
                return ToolResult<AddressablesGroupsResult>.Fail(
                    "Cannot delete the default group.",
                    ErrorCodes.NOT_PERMITTED);

            // Optionally move entries to default group before deleting
            if (moveEntries && group.entries.Count > 0)
            {
                var defaultGroup = settings.DefaultGroup;
                var entries = group.entries.ToList();
                foreach (var entry in entries)
                {
                    settings.CreateOrMoveEntry(entry.guid, defaultGroup, readOnly: false, postEvent: false);
                }
            }

            settings.RemoveGroup(group);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.GroupRemoved, null, true);

            return ToolResult<AddressablesGroupsResult>.Ok(new AddressablesGroupsResult
            {
                Action = "delete",
                Groups = null,
                Message = moveEntries
                    ? $"Deleted group '{groupName}'; entries moved to default group."
                    : $"Deleted group '{groupName}'."
            });
        }
    }
}
#endif
