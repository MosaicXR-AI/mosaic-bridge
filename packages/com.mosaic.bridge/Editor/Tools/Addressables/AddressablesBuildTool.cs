#if MOSAIC_HAS_ADDRESSABLES
using System.Diagnostics;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Addressables
{
    public static class AddressablesBuildTool
    {
        [MosaicTool("addressables/build",
                    "Builds addressable content bundles, optionally performing a clean build first",
                    isReadOnly: false)]
        public static ToolResult<AddressablesBuildResult> Build(AddressablesBuildParams p)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return ToolResult<AddressablesBuildResult>.Fail(
                    "Addressable Asset Settings not found. Ensure the Addressables package is configured.",
                    ErrorCodes.NOT_FOUND,
                    "Open Window > Asset Management > Addressables > Groups to initialize settings.");

            if (p.CleanBuild)
            {
                AddressableAssetSettings.CleanPlayerContent(
                    AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder);
            }

            var sw = Stopwatch.StartNew();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult buildResult);
            sw.Stop();

            bool succeeded = string.IsNullOrEmpty(buildResult.Error);
            int fileCount = buildResult.FileRegistry?.GetFilePaths()?.Count() ?? 0;

            return ToolResult<AddressablesBuildResult>.Ok(new AddressablesBuildResult
            {
                BuildSucceeded = succeeded,
                Error = succeeded ? null : buildResult.Error,
                DurationMs = sw.ElapsedMilliseconds,
                FileCount = fileCount,
                CleanBuildPerformed = p.CleanBuild
            });
        }
    }
}
#endif
