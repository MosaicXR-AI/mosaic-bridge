using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Lighting
{
    public static class LightingBakeTool
    {
        [MosaicTool("lighting/bake",
                    "Bakes lightmaps for the current scene",
                    isReadOnly: false)]
        public static ToolResult<LightingBakeResult> Execute(LightingBakeParams p)
        {
            if (Lightmapping.isRunning)
                return ToolResult<LightingBakeResult>.Ok(new LightingBakeResult
                {
                    Started   = false,
                    IsAsync   = true,
                    IsRunning = true,
                    Message   = "Lightmap bake is already in progress"
                });

            bool started;
            if (p.Async)
            {
                started = Lightmapping.BakeAsync();
            }
            else
            {
                started = Lightmapping.Bake();
            }

            return ToolResult<LightingBakeResult>.Ok(new LightingBakeResult
            {
                Started   = started,
                IsAsync   = p.Async,
                IsRunning = Lightmapping.isRunning,
                Message   = started
                    ? (p.Async ? "Lightmap bake started (async)" : "Lightmap bake completed")
                    : "Lightmap bake failed to start"
            });
        }
    }
}
