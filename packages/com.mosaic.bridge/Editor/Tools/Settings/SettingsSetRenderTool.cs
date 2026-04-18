using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Settings
{
    public static class SettingsSetRenderTool
    {
        [MosaicTool("settings/set-render",
                    "Sets the color space and/or render pipeline asset",
                    isReadOnly: false)]
        public static ToolResult<SettingsSetRenderResult> SetRender(SettingsSetRenderParams p)
        {
            var previousColorSpace = PlayerSettings.colorSpace.ToString();
            var newColorSpace      = previousColorSpace;
            var rpChanged          = false;

            // Update color space
            if (!string.IsNullOrEmpty(p.ColorSpace))
            {
                ColorSpace parsed;
                try
                {
                    parsed = (ColorSpace)Enum.Parse(typeof(ColorSpace), p.ColorSpace, ignoreCase: true);
                }
                catch
                {
                    return ToolResult<SettingsSetRenderResult>.Fail(
                        $"Invalid ColorSpace value '{p.ColorSpace}'. Valid values: Linear, Gamma",
                        ErrorCodes.INVALID_PARAM);
                }

                PlayerSettings.colorSpace = parsed;
                newColorSpace = parsed.ToString();
            }

            // Update render pipeline asset
            if (!string.IsNullOrEmpty(p.RenderPipelineAssetPath))
            {
                if (p.RenderPipelineAssetPath.Equals("builtin", StringComparison.OrdinalIgnoreCase))
                {
                    GraphicsSettings.defaultRenderPipeline = null;
                    rpChanged = true;
                }
                else
                {
                    var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(p.RenderPipelineAssetPath);
                    if (asset != null)
                    {
                        GraphicsSettings.defaultRenderPipeline = asset;
                        rpChanged = true;
                    }
                    else
                    {
                        return ToolResult<SettingsSetRenderResult>.Fail(
                            $"RenderPipelineAsset not found at path '{p.RenderPipelineAssetPath}'",
                            ErrorCodes.NOT_FOUND);
                    }
                }
            }

            return ToolResult<SettingsSetRenderResult>.Ok(new SettingsSetRenderResult
            {
                PreviousColorSpace     = previousColorSpace,
                NewColorSpace          = newColorSpace,
                RenderPipelineChanged  = rpChanged
            });
        }
    }
}
