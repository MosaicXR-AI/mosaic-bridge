using UnityEngine;
using UnityEngine.Rendering;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Graphics
{
    public static class GraphicsRenderInfoTool
    {
        [MosaicTool("graphics/render-info",
                    "Returns current render pipeline, color space, graphics API, quality level, and resolution",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<GraphicsRenderInfoResult> RenderInfo(GraphicsRenderInfoParams p)
        {
            // Determine render pipeline
            string renderPipeline = "Built-in";
            var currentPipeline = GraphicsSettings.currentRenderPipeline;
            if (currentPipeline != null)
                renderPipeline = currentPipeline.GetType().Name;

            // Quality level
            string qualityLevel = QualitySettings.names.Length > QualitySettings.GetQualityLevel()
                ? QualitySettings.names[QualitySettings.GetQualityLevel()]
                : "Unknown";

            // Resolution
            var res = Screen.currentResolution;
            string resolution = $"{res.width}x{res.height}@{res.refreshRateRatio}Hz";

            return ToolResult<GraphicsRenderInfoResult>.Ok(new GraphicsRenderInfoResult
            {
                RenderPipeline = renderPipeline,
                ColorSpace = QualitySettings.activeColorSpace.ToString(),
                GraphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                QualityLevel = qualityLevel,
                QualityLevelIndex = QualitySettings.GetQualityLevel(),
                CurrentResolution = resolution,
                GraphicsDeviceName = SystemInfo.graphicsDeviceName,
                GraphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                GraphicsMemorySize = SystemInfo.graphicsMemorySize
            });
        }
    }
}
