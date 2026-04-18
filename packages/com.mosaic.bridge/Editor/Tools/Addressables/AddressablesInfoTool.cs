#if MOSAIC_HAS_ADDRESSABLES
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Addressables
{
    public static class AddressablesInfoTool
    {
        [MosaicTool("addressables/info",
                    "Queries addressable settings, groups, entries, labels, and profile configuration",
                    isReadOnly: true)]
        public static ToolResult<AddressablesInfoResult> Info(AddressablesInfoParams p)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return ToolResult<AddressablesInfoResult>.Fail(
                    "Addressable Asset Settings not found. Ensure the Addressables package is configured.",
                    ErrorCodes.NOT_FOUND,
                    "Open Window > Asset Management > Addressables > Groups to initialize settings.");

            // Collect all labels
            var allLabels = settings.GetLabels()?.ToArray() ?? System.Array.Empty<string>();

            // Resolve active profile
            var activeProfileId = settings.activeProfileId;
            var profileName = settings.profileSettings.GetProfileName(activeProfileId);
            var buildPath = settings.profileSettings.GetValueByName(activeProfileId, "RemoteBuildPath");
            var loadPath = settings.profileSettings.GetValueByName(activeProfileId, "RemoteLoadPath");

            // Filter groups
            var sourceGroups = settings.groups.Where(g => g != null);
            if (!string.IsNullOrEmpty(p.Group))
                sourceGroups = sourceGroups.Where(g => g.Name == p.Group);

            int totalEntries = 0;
            var groupDetails = new List<AddressablesGroupDetail>();

            foreach (var group in sourceGroups)
            {
                var entries = group.entries.AsEnumerable();

                // Filter by label if specified
                if (!string.IsNullOrEmpty(p.Label))
                    entries = entries.Where(e => e.labels.Contains(p.Label));

                var entryInfos = entries.Select(e => new AddressablesEntryInfo
                {
                    Address = e.address,
                    AssetPath = e.AssetPath,
                    Guid = e.guid,
                    Labels = e.labels.ToArray()
                }).ToArray();

                totalEntries += entryInfos.Length;

                // Only include groups that have entries (when label-filtering) or all groups (when not)
                if (string.IsNullOrEmpty(p.Label) || entryInfos.Length > 0)
                {
                    groupDetails.Add(new AddressablesGroupDetail
                    {
                        Name = group.Name,
                        IsDefault = group == settings.DefaultGroup,
                        Entries = entryInfos
                    });
                }
            }

            return ToolResult<AddressablesInfoResult>.Ok(new AddressablesInfoResult
            {
                TotalGroups = groupDetails.Count,
                TotalEntries = totalEntries,
                AllLabels = allLabels,
                ActiveProfileName = profileName,
                BuildPath = buildPath,
                LoadPath = loadPath,
                Groups = groupDetails.ToArray()
            });
        }
    }
}
#endif
