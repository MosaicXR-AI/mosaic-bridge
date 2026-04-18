#if MOSAIC_HAS_ADDRESSABLES
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Addressables
{
    public static class AddressablesMarkTool
    {
        [MosaicTool("addressables/mark",
                    "Marks an asset as addressable, optionally assigning it to a group with labels and a custom address",
                    isReadOnly: false)]
        public static ToolResult<AddressablesMarkResult> Mark(AddressablesMarkParams p)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return ToolResult<AddressablesMarkResult>.Fail(
                    "Addressable Asset Settings not found. Ensure the Addressables package is configured.",
                    ErrorCodes.NOT_FOUND,
                    "Open Window > Asset Management > Addressables > Groups to initialize settings.");

            // Validate asset exists
            var guid = AssetDatabase.AssetPathToGUID(p.AssetPath);
            if (string.IsNullOrEmpty(guid))
                return ToolResult<AddressablesMarkResult>.Fail(
                    $"Asset not found at path '{p.AssetPath}'.",
                    ErrorCodes.NOT_FOUND);

            // Resolve target group
            AddressableAssetGroup group;
            if (!string.IsNullOrEmpty(p.Group))
            {
                group = settings.FindGroup(p.Group);
                if (group == null)
                    return ToolResult<AddressablesMarkResult>.Fail(
                        $"Addressable group '{p.Group}' not found.",
                        ErrorCodes.NOT_FOUND,
                        "Use addressables/groups with Action='create' to create the group first.");
            }
            else
            {
                group = settings.DefaultGroup;
            }

            // Create or move entry into the group
            var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: true);
            if (entry == null)
                return ToolResult<AddressablesMarkResult>.Fail(
                    $"Failed to create addressable entry for '{p.AssetPath}'.",
                    ErrorCodes.INTERNAL_ERROR);

            // Set custom address
            if (!string.IsNullOrEmpty(p.Address))
                entry.address = p.Address;

            // Apply labels
            var appliedLabels = new List<string>();
            if (p.Labels != null)
            {
                foreach (var label in p.Labels)
                {
                    if (string.IsNullOrWhiteSpace(label)) continue;
                    settings.AddLabel(label);
                    entry.SetLabel(label, true);
                    appliedLabels.Add(label);
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);

            return ToolResult<AddressablesMarkResult>.Ok(new AddressablesMarkResult
            {
                Address = entry.address,
                GroupName = group.Name,
                LabelsApplied = appliedLabels.ToArray(),
                AssetPath = p.AssetPath,
                Guid = guid
            });
        }
    }
}
#endif
