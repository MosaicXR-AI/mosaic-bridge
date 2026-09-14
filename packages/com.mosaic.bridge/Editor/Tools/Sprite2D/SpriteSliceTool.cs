#if MOSAIC_HAS_2D_SPRITE
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Sprite2D
{
    public static class SpriteSliceTool
    {
        // O4 §4.1 (G1): "D7's hidden prerequisite (session log: slicing was done by editor
        // script)" — this is the one 2D gap the course could not route around at all. Grid rects
        // are computed in pure C# (texture pixel space is Y-up, origin bottom-left — row 0 of the
        // request is the visually TOP row, so it gets the HIGHEST y, matching tilemap/set-tiles'
        // own AsciiMap convention). ISpriteNameFileIdDataProvider.SetNameFileIdPairs is populated
        // even on a first slice (not just re-slices) since nothing here can assume this asset was
        // never sliced before.
        [MosaicTool("sprite/slice",
                    "Slices a sprite sheet into named sub-sprites. Grid mode: CellSize [w,h] (+ optional Offset, " +
                    "Padding) tiles the whole texture, row 0 = the visually TOP row. Explicit mode: Rects gives " +
                    "each sub-sprite's own [Name, X, Y, W, H] in texture pixel space (Y-up, origin bottom-left) " +
                    "and optional Pivot. Forces TextureType=Sprite and SpriteMode=Multiple first if not already " +
                    "set. Use sprite/info afterward to confirm what was actually sliced.",
                    isReadOnly: false)]
        public static ToolResult<SpriteSliceResult> Execute(SpriteSliceParams p)
        {
            if (string.IsNullOrEmpty(p.Mode))
                return ToolResult<SpriteSliceResult>.Fail("Mode is required. Valid: Grid, Explicit", ErrorCodes.INVALID_PARAM);

            var importer = AssetImporter.GetAtPath(p.AssetPath) as TextureImporter;
            if (importer == null)
                return ToolResult<SpriteSliceResult>.Fail($"No texture found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            bool needsReimport = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                needsReimport = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                needsReimport = true;
            }
            if (needsReimport)
                importer.SaveAndReimport();

            List<SpriteRect> newRects;
            var mode = p.Mode.Trim().ToLowerInvariant();
            if (mode == "grid")
            {
                if (!TryBuildGridRects(importer, p, out newRects, out var gridError))
                    return ToolResult<SpriteSliceResult>.Fail(gridError, ErrorCodes.INVALID_PARAM);
            }
            else if (mode == "explicit")
            {
                if (!TryBuildExplicitRects(p, out newRects, out var explicitError))
                    return ToolResult<SpriteSliceResult>.Fail(explicitError, ErrorCodes.INVALID_PARAM);
            }
            else
            {
                return ToolResult<SpriteSliceResult>.Fail($"Unknown Mode '{p.Mode}'. Valid: Grid, Explicit", ErrorCodes.INVALID_PARAM);
            }

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (dataProvider == null)
                return ToolResult<SpriteSliceResult>.Fail(
                    $"No sprite data provider available for '{p.AssetPath}'.", ErrorCodes.INTERNAL_ERROR);
            dataProvider.InitSpriteEditorDataProvider();

            var rectsArray = newRects.ToArray();
            dataProvider.SetSpriteRects(rectsArray);

            var nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (nameFileIdProvider != null)
            {
                var pairs = rectsArray.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList();
                nameFileIdProvider.SetNameFileIdPairs(pairs);
            }

            dataProvider.Apply();
            (dataProvider.targetObject as AssetImporter)?.SaveAndReimport();

            return ToolResult<SpriteSliceResult>.Ok(new SpriteSliceResult
            {
                AssetPath = p.AssetPath,
                SpriteCount = rectsArray.Length,
                Sprites = rectsArray.Select(r => new SpriteSliceEntry
                {
                    Name = r.name,
                    Rect = new[] { r.rect.x, r.rect.y, r.rect.width, r.rect.height },
                }).ToArray(),
            });
        }

        private static bool TryBuildGridRects(TextureImporter importer, SpriteSliceParams p, out List<SpriteRect> rects, out string error)
        {
            rects = null;
            error = null;
            if (p.CellSize == null || p.CellSize.Length != 2 || p.CellSize[0] <= 0 || p.CellSize[1] <= 0)
            {
                error = "Grid mode requires CellSize as a positive [w, h]";
                return false;
            }
            float offsetX = p.Offset != null && p.Offset.Length == 2 ? p.Offset[0] : 0f;
            float offsetY = p.Offset != null && p.Offset.Length == 2 ? p.Offset[1] : 0f;
            float padX = p.Padding != null && p.Padding.Length == 2 ? p.Padding[0] : 0f;
            float padY = p.Padding != null && p.Padding.Length == 2 ? p.Padding[1] : 0f;
            float cellW = p.CellSize[0], cellH = p.CellSize[1];

            // TextureImporter.GetWidthAndHeight exists but is internal to Unity's own package
            // code — not accessible here. The imported Texture2D's own width/height (public) is
            // the reliable equivalent.
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath);
            int texWidth = texture != null ? texture.width : 0;
            int texHeight = texture != null ? texture.height : 0;
            if (texWidth <= 0 || texHeight <= 0)
            {
                error = $"Could not read texture dimensions for '{importer.assetPath}'";
                return false;
            }

            int cols = Mathf.FloorToInt((texWidth - offsetX + padX) / (cellW + padX));
            int rows = Mathf.FloorToInt((texHeight - offsetY + padY) / (cellH + padY));
            if (cols <= 0 || rows <= 0)
            {
                error = $"CellSize [{cellW},{cellH}] does not fit inside a {texWidth}x{texHeight} texture with the given Offset/Padding";
                return false;
            }

            var baseName = System.IO.Path.GetFileNameWithoutExtension(importer.assetPath);
            rects = new List<SpriteRect>(rows * cols);
            for (int row = 0; row < rows; row++)
            {
                // row 0 is the visually TOP row -> highest y in texture pixel space (Y-up, origin
                // bottom-left) — same top-to-bottom convention tilemap/set-tiles' AsciiMap uses.
                float y = texHeight - offsetY - (row + 1) * cellH - row * padY;
                for (int col = 0; col < cols; col++)
                {
                    float x = offsetX + col * (cellW + padX);
                    int index = row * cols + col;
                    rects.Add(new SpriteRect
                    {
                        name = $"{baseName}_{index}",
                        rect = new Rect(x, y, cellW, cellH),
                        pivot = new Vector2(0.5f, 0.5f),
                        alignment = SpriteAlignment.Custom,
                        spriteID = GUID.Generate(),
                        border = Vector4.zero,
                    });
                }
            }
            return true;
        }

        private static bool TryBuildExplicitRects(SpriteSliceParams p, out List<SpriteRect> rects, out string error)
        {
            rects = null;
            error = null;
            if (p.Rects == null || p.Rects.Length == 0)
            {
                error = "Explicit mode requires a non-empty Rects array";
                return false;
            }
            rects = new List<SpriteRect>(p.Rects.Length);
            foreach (var r in p.Rects)
            {
                if (string.IsNullOrEmpty(r.Name))
                {
                    error = "Every Rects entry requires Name";
                    return false;
                }
                if (r.W <= 0 || r.H <= 0)
                {
                    error = $"Rects entry '{r.Name}' must have W and H both > 0";
                    return false;
                }
                var pivot = r.Pivot != null && r.Pivot.Length == 2
                    ? new Vector2(r.Pivot[0], r.Pivot[1])
                    : new Vector2(0.5f, 0.5f);
                rects.Add(new SpriteRect
                {
                    name = r.Name,
                    rect = new Rect(r.X, r.Y, r.W, r.H),
                    pivot = pivot,
                    alignment = SpriteAlignment.Custom,
                    spriteID = GUID.Generate(),
                    border = Vector4.zero,
                });
            }
            return true;
        }
    }
}
#endif
