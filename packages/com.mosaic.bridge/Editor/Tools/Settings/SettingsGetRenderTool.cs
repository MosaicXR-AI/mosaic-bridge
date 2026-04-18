using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Settings
{
    public static class SettingsGetRenderTool
    {
        [MosaicTool("settings/get-render",
                    "Returns the current render pipeline, color space, and graphics API settings",
                    isReadOnly: true)]
        public static ToolResult<SettingsGetRenderResult> GetRender(SettingsGetRenderParams p)
        {
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

            // Render pipeline asset name
            var rpa = GraphicsSettings.defaultRenderPipeline;
            var rpName = rpa != null ? rpa.name : "Built-in";

            // Color space
            var colorSpace = PlayerSettings.colorSpace.ToString();

            // HDR display (may not exist in all Unity versions)
            bool hdrEnabled = false;
            try
            {
#pragma warning disable CS0618
                hdrEnabled = PlayerSettings.useHDRDisplay;
#pragma warning restore CS0618
            }
            catch { /* not available in this Unity version */ }

            // First preferred graphics API
            string graphicsApi = "Unknown";
            try
            {
                var apis = PlayerSettings.GetGraphicsAPIs(activeBuildTarget);
                if (apis != null && apis.Length > 0)
                    graphicsApi = apis[0].ToString();
            }
            catch { /* ignore */ }

            return ToolResult<SettingsGetRenderResult>.Ok(new SettingsGetRenderResult
            {
                RenderPipelineAsset = rpName,
                ColorSpace          = colorSpace,
                HdrEnabled          = hdrEnabled,
                ActiveBuildTarget   = activeBuildTarget.ToString(),
                GraphicsApi         = graphicsApi
            });
        }
    }
}
