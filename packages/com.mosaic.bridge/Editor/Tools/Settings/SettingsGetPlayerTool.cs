using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Settings
{
    public static class SettingsGetPlayerTool
    {
        [MosaicTool("settings/get-player",
                    "Returns player settings: company name, product name, version, bundle ID, scripting backend, and API compatibility level",
                    isReadOnly: true)]
        public static ToolResult<SettingsGetPlayerResult> GetPlayer(SettingsGetPlayerParams p)
        {
            var activeBuildTarget      = EditorUserBuildSettings.activeBuildTarget;
            var activeBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(activeBuildTarget);

            return ToolResult<SettingsGetPlayerResult>.Ok(new SettingsGetPlayerResult
            {
                CompanyName            = PlayerSettings.companyName,
                ProductName            = PlayerSettings.productName,
                Version                = PlayerSettings.bundleVersion,
                BundleIdentifier       = PlayerSettings.GetApplicationIdentifier(activeBuildTargetGroup),
                ScriptingBackend       = PlayerSettings.GetScriptingBackend(activeBuildTargetGroup).ToString(),
                ApiCompatibilityLevel  = PlayerSettings.GetApiCompatibilityLevel(activeBuildTargetGroup).ToString(),
                ActiveBuildTarget      = activeBuildTarget.ToString()
            });
        }
    }
}
