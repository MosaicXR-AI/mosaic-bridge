using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Lighting
{
    public static class LightingBakeStatusTool
    {
        // G6: lighting/bake starts a bake but has no way to poll it, so an agent has no honest
        // answer to "is it done yet" beyond guessing from wall-clock time. This ships standalone
        // ahead of the O4 §3.4 job registry migration — Lightmapping already exposes durable,
        // polling-safe state (isRunning, buildProgress) with no registry needed to read it.
        [MosaicTool("lighting/bake-status",
                    "Reports whether the lightmap bake started by lighting/bake is still running, its progress " +
                    "(0..1, only meaningful while running), and how many lightmaps currently exist as durable " +
                    "evidence a bake actually completed.",
                    isReadOnly: true)]
        public static ToolResult<LightingBakeStatusResult> Execute(LightingBakeStatusParams p)
        {
            bool running = Lightmapping.isRunning;
            float progress = running ? Lightmapping.buildProgress : 0f;
            int lightmapCount = LightmapSettings.lightmaps != null ? LightmapSettings.lightmaps.Length : 0;

            return ToolResult<LightingBakeStatusResult>.Ok(new LightingBakeStatusResult
            {
                IsRunning = running,
                Progress = progress,
                LightmapCount = lightmapCount,
                Message = running
                    ? $"Baking… {progress:P0}"
                    : $"Idle — {lightmapCount} lightmap(s) present"
            });
        }
    }
}
