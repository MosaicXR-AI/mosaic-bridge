using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainCreateTool
    {
        [MosaicTool("terrain/create",
                    "Creates a new Terrain GameObject with a saved TerrainData asset",
                    isReadOnly: false)]
        public static ToolResult<TerrainCreateResult> Execute(TerrainCreateParams p)
        {
            if (p.Width <= 0)
                return ToolResult<TerrainCreateResult>.Fail(
                    "Width must be greater than 0", ErrorCodes.INVALID_PARAM);
            if (p.Length <= 0)
                return ToolResult<TerrainCreateResult>.Fail(
                    "Length must be greater than 0", ErrorCodes.INVALID_PARAM);
            if (p.Height <= 0)
                return ToolResult<TerrainCreateResult>.Fail(
                    "Height must be greater than 0", ErrorCodes.INVALID_PARAM);

            int resolution = TerrainToolHelpers.ClampHeightmapResolution(p.HeightmapResolution);

            // Create TerrainData
            var terrainData = new TerrainData();
            terrainData.heightmapResolution = resolution;
            terrainData.size = new Vector3(p.Width, p.Height, p.Length);

            // Save TerrainData as asset
            string assetPath = TerrainToolHelpers.GetTerrainDataAssetPath(p.Name);
            AssetDatabase.CreateAsset(terrainData, assetPath);
            AssetDatabase.SaveAssets();

            // Create Terrain GameObject
            var go = UnityEngine.Terrain.CreateTerrainGameObject(terrainData);
            go.name = p.Name;

            // Position
            if (p.Position != null && p.Position.Length == 3)
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

            // Register with Undo
            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create Terrain");

            return ToolResult<TerrainCreateResult>.Ok(new TerrainCreateResult
            {
                InstanceId          = go.GetInstanceID(),
                Name                = go.name,
                Width               = p.Width,
                Length              = p.Length,
                Height              = p.Height,
                HeightmapResolution = resolution,
                TerrainDataAssetPath = assetPath
            });
        }
    }
}
