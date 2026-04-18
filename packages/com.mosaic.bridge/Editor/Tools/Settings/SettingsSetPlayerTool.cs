using System.Collections.Generic;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Settings
{
    public static class SettingsSetPlayerTool
    {
        [MosaicTool("settings/set-player",
                    "Updates player settings: company name, product name, version, and/or bundle identifier",
                    isReadOnly: false)]
        public static ToolResult<SettingsSetPlayerResult> SetPlayer(SettingsSetPlayerParams p)
        {
            var changed = new List<string>();

            if (p.CompanyName != null)
            {
                PlayerSettings.companyName = p.CompanyName;
                changed.Add("CompanyName");
            }

            if (p.ProductName != null)
            {
                PlayerSettings.productName = p.ProductName;
                changed.Add("ProductName");
            }

            if (p.Version != null)
            {
                PlayerSettings.bundleVersion = p.Version;
                changed.Add("Version");
            }

            if (p.BundleIdentifier != null)
            {
                PlayerSettings.SetApplicationIdentifier(
                    BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                    p.BundleIdentifier);
                changed.Add("BundleIdentifier");
            }

            AssetDatabase.SaveAssets();

            return ToolResult<SettingsSetPlayerResult>.Ok(new SettingsSetPlayerResult
            {
                Changed = changed.ToArray()
            });
        }
    }
}
