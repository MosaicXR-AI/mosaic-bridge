using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Settings
{
    public static class SettingsGetQualityTool
    {
        [MosaicTool("settings/get-quality",
                    "Returns the available quality levels and the currently active level",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<SettingsGetQualityResult> GetQuality(SettingsGetQualityParams p)
        {
            var names        = QualitySettings.names;
            var currentIndex = QualitySettings.GetQualityLevel();

            return ToolResult<SettingsGetQualityResult>.Ok(new SettingsGetQualityResult
            {
                LevelNames        = names,
                CurrentLevelIndex = currentIndex,
                CurrentLevelName  = names.Length > 0 ? names[currentIndex] : string.Empty,
                VsyncOptions      = new[]
                {
                    "Don't Sync (0)",
                    "Every V Blank (1)",
                    "Every Second V Blank (2)"
                }
            });
        }
    }
}
