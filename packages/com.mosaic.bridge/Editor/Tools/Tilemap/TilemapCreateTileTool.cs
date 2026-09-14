using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Assets;

namespace Mosaic.Bridge.Tools.Tilemap
{
    public static class TilemapCreateTileTool
    {
        // O4 §4.1 (G1): scriptableobject/create could already make an empty Tile, but set-field had
        // no ObjectReference support at all (fixed by §3.1) and still could not batch a whole sliced
        // sheet into one Tile asset per sub-sprite in one call — the actual shape a Ground/Wall
        // tileset needs.
        [MosaicTool("tilemap/create-tile",
                    "Creates a Tile asset from a sprite. Single mode: SpritePath (optionally " +
                    "'Assets/sheet.png#SubSprite') + AssetPath. Batch mode: SpriteSheetPath (a sliced sheet) + " +
                    "OutputFolder — creates one Tile per sub-sprite, named after it. ColliderType='Grid' makes " +
                    "square tiles solid (pairs with tilemap/add-collider); 'Sprite' follows the sprite's physics " +
                    "shape; 'None' (default) is visual-only.",
                    isReadOnly: false)]
        public static ToolResult<TilemapCreateTileResult> Execute(TilemapCreateTileParams p)
        {
            if (!TryParseColliderType(p.ColliderType, out var colliderType))
                return ToolResult<TilemapCreateTileResult>.Fail(
                    $"Unknown ColliderType '{p.ColliderType}'. Valid: None, Sprite, Grid", ErrorCodes.INVALID_PARAM);

            Color tint = Color.white;
            if (p.Color != null)
            {
                if (p.Color.Length < 3)
                    return ToolResult<TilemapCreateTileResult>.Fail(
                        "Color requires a [r, g, b] or [r, g, b, a] float array", ErrorCodes.INVALID_PARAM);
                tint = new Color(p.Color[0], p.Color[1], p.Color[2], p.Color.Length >= 4 ? p.Color[3] : 1f);
            }

            bool hasSingle = !string.IsNullOrEmpty(p.SpritePath) || !string.IsNullOrEmpty(p.AssetPath);
            bool hasBatch = !string.IsNullOrEmpty(p.SpriteSheetPath) || !string.IsNullOrEmpty(p.OutputFolder);
            if (hasSingle == hasBatch)
                return ToolResult<TilemapCreateTileResult>.Fail(
                    "Provide either (SpritePath + AssetPath) for a single tile, or (SpriteSheetPath + " +
                    "OutputFolder) to batch one tile per sub-sprite — not both, not neither.",
                    ErrorCodes.INVALID_PARAM);

            if (hasSingle)
            {
                if (string.IsNullOrEmpty(p.SpritePath) || string.IsNullOrEmpty(p.AssetPath))
                    return ToolResult<TilemapCreateTileResult>.Fail(
                        "Single mode requires both SpritePath and AssetPath", ErrorCodes.INVALID_PARAM);

                if (!ObjectReferenceResolver.TryResolveAsset(p.SpritePath, typeof(Sprite), out var resolved, out var error))
                    return ToolResult<TilemapCreateTileResult>.Fail(error, ErrorCodes.NOT_FOUND);

                var info = CreateTileAsset((Sprite)resolved, p.AssetPath, colliderType, tint);
                return ToolResult<TilemapCreateTileResult>.Ok(new TilemapCreateTileResult { Tiles = new[] { info } });
            }

            if (string.IsNullOrEmpty(p.SpriteSheetPath) || string.IsNullOrEmpty(p.OutputFolder))
                return ToolResult<TilemapCreateTileResult>.Fail(
                    "Batch mode requires both SpriteSheetPath and OutputFolder", ErrorCodes.INVALID_PARAM);
            if (!AssetDatabase.IsValidFolder(p.OutputFolder))
                return ToolResult<TilemapCreateTileResult>.Fail(
                    $"OutputFolder '{p.OutputFolder}' does not exist. Create it first.", ErrorCodes.INVALID_PARAM);

            var sprites = AssetDatabase.LoadAllAssetsAtPath(p.SpriteSheetPath).OfType<Sprite>().ToArray();
            if (sprites.Length == 0)
                return ToolResult<TilemapCreateTileResult>.Fail(
                    $"'{p.SpriteSheetPath}' has no sliced sub-sprites (use sprite/slice first).", ErrorCodes.NOT_FOUND);

            var created = new List<TileAssetInfo>();
            foreach (var sprite in sprites)
            {
                var assetPath = $"{p.OutputFolder}/{sprite.name}.asset";
                created.Add(CreateTileAsset(sprite, assetPath, colliderType, tint));
            }

            return ToolResult<TilemapCreateTileResult>.Ok(new TilemapCreateTileResult { Tiles = created.ToArray() });
        }

        private static TileAssetInfo CreateTileAsset(Sprite sprite, string assetPath, Tile.ColliderType colliderType, Color tint)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = tint;
            tile.colliderType = colliderType;
            AssetDatabase.CreateAsset(tile, assetPath);

            return new TileAssetInfo
            {
                AssetPath = assetPath,
                SpriteName = sprite.name,
                ColliderType = colliderType.ToString(),
            };
        }

        private static bool TryParseColliderType(string value, out Tile.ColliderType result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case null:
                case "":
                case "none":   result = Tile.ColliderType.None;   return true;
                case "sprite": result = Tile.ColliderType.Sprite; return true;
                case "grid":   result = Tile.ColliderType.Grid;   return true;
                default:       result = Tile.ColliderType.None;   return false;
            }
        }
    }
}
