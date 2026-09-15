using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Lighting
{
    public static class LightingBakeCancelTool
    {
        [MosaicTool("lighting/bake-cancel",
                    "Cancels an in-progress lightmap bake started by lighting/bake. A no-op (still " +
                    "succeeds) when no bake is running.",
                    isReadOnly: false)]
        public static ToolResult<LightingBakeCancelResult> Execute(LightingBakeCancelParams p)
        {
            var wasRunning = Lightmapping.isRunning;
            if (wasRunning)
                Lightmapping.Cancel();

            return ToolResult<LightingBakeCancelResult>.Ok(new LightingBakeCancelResult
            {
                WasRunning = wasRunning,
            });
        }
    }
}
