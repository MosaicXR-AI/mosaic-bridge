using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainGridTool
    {
        [MosaicTool("terrain/grid",
                    "Creates a multi-terrain grid with automatic neighbor connections",
                    isReadOnly: false)]
        public static ToolResult<TerrainGridResult> Execute(TerrainGridParams p)
        {
            if (p.Rows <= 0)
                return ToolResult<TerrainGridResult>.Fail(
                    "Rows must be greater than 0", ErrorCodes.INVALID_PARAM);
            if (p.Columns <= 0)
                return ToolResult<TerrainGridResult>.Fail(
                    "Columns must be greater than 0", ErrorCodes.INVALID_PARAM);
            if (p.TileWidth <= 0 || p.TileLength <= 0 || p.TileHeight <= 0)
                return ToolResult<TerrainGridResult>.Fail(
                    "TileWidth, TileLength, and TileHeight must be greater than 0",
                    ErrorCodes.INVALID_PARAM);

            int resolution = TerrainToolHelpers.ClampHeightmapResolution(p.HeightmapResolution);
            string prefix = string.IsNullOrEmpty(p.NamePrefix) ? "Terrain" : p.NamePrefix;

            var terrains = new UnityEngine.Terrain[p.Rows, p.Columns];
            var instanceIds = new List<int>();
            var names = new List<string>();

            // Create all terrain tiles
            for (int row = 0; row < p.Rows; row++)
            {
                for (int col = 0; col < p.Columns; col++)
                {
                    string tileName = $"{prefix}_{row}_{col}";

                    var terrainData = new TerrainData();
                    terrainData.heightmapResolution = resolution;
                    terrainData.size = new Vector3(p.TileWidth, p.TileHeight, p.TileLength);

                    string assetPath = TerrainToolHelpers.GetTerrainDataAssetPath(tileName);
                    AssetDatabase.CreateAsset(terrainData, assetPath);

                    var go = UnityEngine.Terrain.CreateTerrainGameObject(terrainData);
                    go.name = tileName;
                    go.transform.position = new Vector3(
                        col * p.TileWidth,
                        0f,
                        row * p.TileLength);

                    Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create Terrain Grid");

                    terrains[row, col] = go.GetComponent<UnityEngine.Terrain>();
                    instanceIds.Add(go.GetInstanceID());
                    names.Add(tileName);
                }
            }

            AssetDatabase.SaveAssets();

            // Set neighbors for seamless stitching
            for (int row = 0; row < p.Rows; row++)
            {
                for (int col = 0; col < p.Columns; col++)
                {
                    var left   = col > 0             ? terrains[row, col - 1] : null;
                    var right  = col < p.Columns - 1 ? terrains[row, col + 1] : null;
                    var top    = row < p.Rows - 1    ? terrains[row + 1, col] : null;
                    var bottom = row > 0             ? terrains[row - 1, col] : null;

                    terrains[row, col].SetNeighbors(left, top, right, bottom);
                }
            }

            int total = p.Rows * p.Columns;

            return ToolResult<TerrainGridResult>.Ok(new TerrainGridResult
            {
                Rows       = p.Rows,
                Columns    = p.Columns,
                TotalTiles = total,
                InstanceIds = instanceIds.ToArray(),
                Names      = names.ToArray(),
                Message    = $"Created {p.Rows}x{p.Columns} terrain grid ({total} tiles) with neighbors connected"
            });
        }
    }
}
