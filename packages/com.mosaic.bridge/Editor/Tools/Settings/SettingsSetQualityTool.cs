using System.Linq;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Settings
{
    public static class SettingsSetQualityTool
    {
        [MosaicTool("settings/set-quality",
                    "Sets the active quality level by index or name",
                    isReadOnly: false)]
        public static ToolResult<SettingsSetQualityResult> SetQuality(SettingsSetQualityParams p)
        {
            if (p.LevelIndex == null && string.IsNullOrEmpty(p.LevelName))
                return ToolResult<SettingsSetQualityResult>.Fail(
                    "Either LevelIndex or LevelName must be provided",
                    ErrorCodes.INVALID_PARAM);

            var names = QualitySettings.names;

            int targetIndex;
            if (p.LevelIndex.HasValue)
            {
                targetIndex = p.LevelIndex.Value;
            }
            else
            {
                targetIndex = -1;
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] == p.LevelName)
                    {
                        targetIndex = i;
                        break;
                    }
                }

                if (targetIndex < 0)
                    return ToolResult<SettingsSetQualityResult>.Fail(
                        $"Quality level '{p.LevelName}' not found. Available: {string.Join(", ", names)}",
                        ErrorCodes.NOT_FOUND);
            }

            if (targetIndex < 0 || targetIndex >= names.Length)
                return ToolResult<SettingsSetQualityResult>.Fail(
                    $"LevelIndex {targetIndex} is out of range [0, {names.Length - 1}]",
                    ErrorCodes.OUT_OF_RANGE);

            var previousLevel = names[QualitySettings.GetQualityLevel()];
            QualitySettings.SetQualityLevel(targetIndex, p.ApplyExpensiveChanges);

            return ToolResult<SettingsSetQualityResult>.Ok(new SettingsSetQualityResult
            {
                PreviousLevel = previousLevel,
                NewLevel      = names[targetIndex],
                NewLevelIndex = targetIndex
            });
        }
    }
}
