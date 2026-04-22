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
                    "Returns the active render pipeline, color space, and graphics API settings. Pipeline field is one of: BuiltIn, URP, HDRP, Custom.",
                    isReadOnly: true)]
        public static ToolResult<SettingsGetRenderResult> GetRender(SettingsGetRenderParams p)
        {
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

            // QualitySettings.renderPipeline is the per-quality-level override (Unity 6+).
            // Fall back to GraphicsSettings.currentRenderPipeline (the project-level default)
            // when the quality level has no override. This order is correct on all Unity versions:
            // Unity 6 deprecated currentRenderPipeline in contexts where quality overrides exist.
            var rpa = QualitySettings.renderPipeline ?? GraphicsSettings.currentRenderPipeline;
            var rpName = rpa != null ? rpa.name : "Built-in";
            var rpType = rpa != null ? rpa.GetType().Name : "Built-in";
            var canonicalPipeline = DetectPipeline(rpa);

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
                Pipeline                = canonicalPipeline,
                RenderPipelineAssetType = rpType,
                RenderPipelineAsset     = rpName,
                ColorSpace              = colorSpace,
                HdrEnabled              = hdrEnabled,
                ActiveBuildTarget       = activeBuildTarget.ToString(),
                GraphicsApi             = graphicsApi
            });
        }

        private static string DetectPipeline(RenderPipelineAsset rpa)
        {
            if (rpa == null) return "BuiltIn";
            var typeName = rpa.GetType().Name;
            if (typeName == "UniversalRenderPipelineAsset") return "URP";
            if (typeName == "HDRenderPipelineAsset") return "HDRP";
            return "Custom";
        }
    }
}
