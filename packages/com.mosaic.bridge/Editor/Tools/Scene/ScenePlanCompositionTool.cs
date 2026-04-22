using System;
using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Scene
{
    public static class ScenePlanCompositionTool
    {
        [MosaicTool("scene/plan-composition",
                    "Takes a structured scene intent (geographic reference, regions, landmarks, time of day) " +
                    "and returns a validated execution plan: resolved world-space Y coordinates for every landmark " +
                    "(sampled from the active terrain), ordered build phases, lighting parameters, and a camera " +
                    "start position. Run this after terrain/create + terrain/height sculpting and BEFORE placing " +
                    "any objects — it eliminates the Y=0 stacking failure by providing pre-resolved coordinates. " +
                    "Output ObjectPlacements[*].WorldY is the value to pass as Position[1] in gameobject/create.",
                    isReadOnly: true)]
        public static ToolResult<ScenePlanCompositionResult> Execute(ScenePlanCompositionParams p)
        {
            var warnings = new List<string>();
            var planId   = Guid.NewGuid().ToString("N").Substring(0, 8);

            // ── Terrain: try to sample existing terrain for Y resolution ──────
            var terrain = UnityEngine.Terrain.activeTerrain;
            if (p.SampleExistingTerrain && terrain == null)
                warnings.Add("No active terrain found — landmark Y will use region HeightRangeMin. " +
                             "Create and sculpt terrain first, then re-run scene/plan-composition for accurate Y.");

            // ── Resolve landmark placements ────────────────────────────────────
            var placements = new List<ScenePlacedObject>();
            if (p.Regions != null)
            {
                foreach (var region in p.Regions)
                {
                    if (region.Landmarks == null) continue;
                    foreach (var lm in region.Landmarks)
                    {
                        float cx = lm.PreferredX ?? (region.XMin + region.XMax) * 0.5f;
                        float cz = lm.PreferredZ ?? (region.ZMin + region.ZMax) * 0.5f;
                        float terrainH = 0f;
                        bool  sampled  = false;

                        if (p.SampleExistingTerrain && terrain != null)
                        {
                            terrainH = terrain.SampleHeight(new Vector3(cx, 0f, cz));
                            sampled  = true;
                        }
                        else
                        {
                            terrainH = region.HeightRangeMin;
                        }

                        float worldY = terrainH + lm.YOffset;

                        placements.Add(new ScenePlacedObject
                        {
                            LandmarkName  = lm.Name,
                            RegionId      = region.Id,
                            WorldX        = cx,
                            WorldY        = worldY,
                            WorldZ        = cz,
                            TerrainHeight = terrainH,
                            YOffset       = lm.YOffset,
                            HeightSampled = sampled
                        });
                    }
                }
            }

            // ── Lighting from timeOfDay ────────────────────────────────────────
            var lighting = ResolveLighting(p.TimeOfDay, p.Weather);

            // ── Camera start: behind terrain center, elevated ─────────────────
            float camX = p.TerrainSizeX * 0.5f;
            float camZ = -50f;
            float camY = p.MaxHeightMeters * 0.5f + 20f;
            float lookX = p.TerrainSizeX * 0.5f;
            float lookY = p.MaxHeightMeters * 0.2f;
            float lookZ = p.TerrainSizeZ * 0.5f;

            if (p.PlayerType == "drone")
            {
                camY  = Mathf.Max(50f, p.MaxHeightMeters * 0.3f);
                camZ  = p.TerrainSizeZ * 0.1f;
            }
            else if (p.PlayerType == "top_down")
            {
                camY  = p.MaxHeightMeters + p.TerrainSizeZ * 0.5f;
                camZ  = p.TerrainSizeZ * 0.5f;
                lookZ = p.TerrainSizeZ * 0.5f;
                lookY = 0f;
            }

            // ── Validate render pipeline hint ─────────────────────────────────
            if (!string.IsNullOrEmpty(p.RenderPipeline) &&
                p.RenderPipeline != "URP" && p.RenderPipeline != "HDRP" && p.RenderPipeline != "BuiltIn")
                warnings.Add($"RenderPipeline '{p.RenderPipeline}' is not a recognised value. Expected: URP, HDRP, BuiltIn.");

            // ── Execution phases ──────────────────────────────────────────────
            var phases = BuildExecutionPhases(p);

            return ToolResult<ScenePlanCompositionResult>.Ok(new ScenePlanCompositionResult
            {
                PlanId          = planId,
                SceneName       = p.SceneName,
                GeographicRef   = p.GeographicRef,
                Warnings        = warnings.ToArray(),
                Lighting        = lighting,
                CameraStart     = new[] { camX, camY, camZ },
                CameraLookAt    = new[] { lookX, lookY, lookZ },
                ObjectPlacements = placements.ToArray(),
                ExecutionPhases  = phases
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static SceneLightingParams ResolveLighting(string timeOfDay, string weather)
        {
            float angle    = 60f;
            float[] color  = { 1f, 0.96f, 0.84f };
            float intensity = 1.2f;
            string skybox  = "procedural";
            float fog      = 0.001f;

            switch (timeOfDay?.ToLowerInvariant())
            {
                case "dawn":
                case "sunrise":
                    angle = 5f; color = new[]{ 1f, 0.7f, 0.4f }; intensity = 0.6f; skybox = "sunrise"; fog = 0.003f;
                    break;
                case "morning":
                    angle = 30f; color = new[]{ 1f, 0.92f, 0.75f }; intensity = 1.0f;
                    break;
                case "midday":
                case "noon":
                    angle = 75f; color = new[]{ 1f, 0.98f, 0.95f }; intensity = 1.4f;
                    break;
                case "golden hour":
                case "goldenhour":
                case "afternoon":
                    angle = 15f; color = new[]{ 1f, 0.72f, 0.35f }; intensity = 0.9f; skybox = "golden-hour"; fog = 0.002f;
                    break;
                case "dusk":
                case "sunset":
                    angle = 3f; color = new[]{ 1f, 0.45f, 0.2f }; intensity = 0.5f; skybox = "sunset"; fog = 0.004f;
                    break;
                case "night":
                    angle = -20f; color = new[]{ 0.5f, 0.55f, 0.8f }; intensity = 0.08f; skybox = "starry-night"; fog = 0.002f;
                    break;
            }

            if (weather?.ToLowerInvariant().Contains("hazy") == true ||
                weather?.ToLowerInvariant().Contains("dust") == true)
                fog = Mathf.Max(fog, 0.004f);
            else if (weather?.ToLowerInvariant().Contains("fog") == true ||
                     weather?.ToLowerInvariant().Contains("overcast") == true)
            {
                fog = 0.015f; intensity *= 0.7f;
            }
            else if (weather?.ToLowerInvariant().Contains("rain") == true)
            {
                fog = 0.012f; intensity *= 0.5f; color = new[]{ 0.8f, 0.82f, 0.9f };
            }

            return new SceneLightingParams
            {
                DirectionalAngle = angle,
                DirectionalColor = color,
                Intensity        = intensity,
                SkyboxPreset     = skybox,
                FogDensity       = fog
            };
        }

        private static SceneExecutionPhase[] BuildExecutionPhases(ScenePlanCompositionParams p)
        {
            var phases = new List<SceneExecutionPhase>
            {
                new SceneExecutionPhase { Phase = 1, Name = "Scene + Terrain Foundation",
                    ToolHints = new[]{"scene/create", "terrain/create", "terrain/height (sculpt major features)", "terrain/height (secondary detail noise)"},
                    Note = "Terrain defines the coordinate space. All subsequent Y coords are relative to it." },

                new SceneExecutionPhase { Phase = 2, Name = "Terrain Textures",
                    ToolHints = new[]{"terrain/paint (add-layer for each biome texture)", "terrain/paint (paint-layer for each region)", "terrain/paint (fill-layer for base)"},
                    Note = "Paint before vegetation — splat layout is the base visual." },

                new SceneExecutionPhase { Phase = 3, Name = "Sky + Lighting",
                    ToolHints = new[]{ $"lighting/set-directional (angle={p.TimeOfDay})", "lighting/set-skybox", "lighting/set-ambient", "settings/get-render (verify pipeline)" },
                    Note = "Lighting affects how materials and vegetation look. Set before adding props." },

                new SceneExecutionPhase { Phase = 4, Name = "Large Structures + Landmarks",
                    ToolHints = new[]{"terrain/sample-height (for each landmark XZ → get Y)", "gameobject/create (use resolved Y from ObjectPlacements)", "prefab/instantiate"},
                    Note = "Use ObjectPlacements[*].WorldY from this plan for placement — never hardcode Y=0." },

                new SceneExecutionPhase { Phase = 5, Name = "Vegetation",
                    ToolHints = new[]{"terrain/trees (add-prototype then paint-trees)", "terrain/grass (add-detail then paint-detail)"},
                    Note = "Unity terrain trees must have MeshRenderer/LODGroup/BillboardRenderer on the root object." },

                new SceneExecutionPhase { Phase = 6, Name = "Post-Processing + Atmosphere",
                    ToolHints = new[]{ p.RenderPipeline == "HDRP" ? "hdrp/volume" : "urp/volume", "graphics/set-post-processing (bloom, color grade, depth of field)", "render/atmosphere (fog)" },
                    Note = "Final visual pass. Apply last after all geometry is in place." },
            };

            if (p.PlayerType != "none")
            {
                string controllerHint = p.PlayerType switch
                {
                    "drone"    => "script/create (drone controller: smooth accel/decel, yaw/pitch, altitude)",
                    "fps"      => "script/create (FPS controller: CharacterController + mouse look)",
                    "tps"      => "script/create (TPS controller: follow camera + character mover)",
                    "top_down" => "script/create (top-down controller: WASD pan + scroll zoom)",
                    _          => "script/create (player controller)"
                };

                phases.Add(new SceneExecutionPhase
                {
                    Phase     = 7,
                    Name      = "Camera + Player Controller",
                    ToolHints = new[]{ "camera/create", controllerHint },
                    Note      = $"Camera calibrated last. Start position: {CameraStartNote(p)}."
                });
            }

            return phases.ToArray();
        }

        private static string CameraStartNote(ScenePlanCompositionParams p)
        {
            float camX = p.TerrainSizeX * 0.5f;
            float camZ = p.PlayerType == "drone" ? p.TerrainSizeZ * 0.1f : -50f;
            float camY = p.PlayerType == "top_down"
                ? p.MaxHeightMeters + p.TerrainSizeZ * 0.5f
                : Mathf.Max(50f, p.MaxHeightMeters * 0.3f);
            return $"({camX:F0}, {camY:F0}, {camZ:F0})";
        }
    }
}
