using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainSettingsTool
    {
        [MosaicTool("terrain/settings",
                    "Gets or sets terrain rendering and LOD settings",
                    isReadOnly: false)]
        public static ToolResult<TerrainSettingsResult> Execute(TerrainSettingsParams p)
        {
            var terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.Name, out string error);
            if (terrain == null)
                return ToolResult<TerrainSettingsResult>.Fail(error, ErrorCodes.NOT_FOUND);

            bool changed = false;

            Undo.RecordObject(terrain, "Mosaic: Terrain Settings");

            if (p.BasemapDistance.HasValue)
            {
                terrain.basemapDistance = p.BasemapDistance.Value;
                changed = true;
            }
            if (p.DetailObjectDistance.HasValue)
            {
                terrain.detailObjectDistance = p.DetailObjectDistance.Value;
                changed = true;
            }
            if (p.DetailObjectDensity.HasValue)
            {
                terrain.detailObjectDensity = p.DetailObjectDensity.Value;
                changed = true;
            }
            if (p.TreeDistance.HasValue)
            {
                terrain.treeDistance = p.TreeDistance.Value;
                changed = true;
            }
            if (p.TreeBillboardDistance.HasValue)
            {
                terrain.treeBillboardDistance = p.TreeBillboardDistance.Value;
                changed = true;
            }
            if (p.TreeMaximumFullLODCount.HasValue)
            {
                terrain.treeMaximumFullLODCount = p.TreeMaximumFullLODCount.Value;
                changed = true;
            }
            if (p.HeightmapPixelError.HasValue)
            {
                terrain.heightmapPixelError = p.HeightmapPixelError.Value;
                changed = true;
            }
            if (p.CastShadows.HasValue)
            {
                terrain.shadowCastingMode = p.CastShadows.Value
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                changed = true;
            }
            if (p.DrawHeightmap.HasValue)
            {
                terrain.drawHeightmap = p.DrawHeightmap.Value;
                changed = true;
            }
            if (p.DrawTreesAndFoliage.HasValue)
            {
                terrain.drawTreesAndFoliage = p.DrawTreesAndFoliage.Value;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(terrain);

            return ToolResult<TerrainSettingsResult>.Ok(new TerrainSettingsResult
            {
                InstanceId            = terrain.gameObject.GetInstanceID(),
                Name                  = terrain.gameObject.name,
                BasemapDistance        = terrain.basemapDistance,
                DetailObjectDistance   = terrain.detailObjectDistance,
                DetailObjectDensity   = terrain.detailObjectDensity,
                TreeDistance           = terrain.treeDistance,
                TreeBillboardDistance  = terrain.treeBillboardDistance,
                TreeMaximumFullLODCount = terrain.treeMaximumFullLODCount,
                HeightmapPixelError   = terrain.heightmapPixelError,
                CastShadows           = terrain.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off,
                DrawHeightmap         = terrain.drawHeightmap,
                DrawTreesAndFoliage   = terrain.drawTreesAndFoliage,
                Message               = changed ? "Settings updated" : "No changes (read-only query)"
            });
        }
    }
}
