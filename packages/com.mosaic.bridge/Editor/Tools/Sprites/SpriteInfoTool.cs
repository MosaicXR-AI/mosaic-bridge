using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Sprites
{
    public static class SpriteInfoTool
    {
        // O4 §4.1 (G1): "prerequisite for every sprite assignment" and the verification step for
        // sprite/slice — an agent cannot address a sub-sprite by name ("Assets/sheet.png#Run_03")
        // without first being able to ask what names actually exist on a sheet.
        [MosaicTool("sprite/info",
                    "Lists every sub-sprite on a texture asset (a sliced sheet) or the single sprite (Single " +
                    "import mode), with each one's rect, pivot, pixels-per-unit, and border. Sprite.name is what " +
                    "'AssetPath#Name' sub-asset addressing (O4 §3.1) expects.",
                    isReadOnly: true)]
        public static ToolResult<SpriteInfoResult> Execute(SpriteInfoParams p)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(p.AssetPath).OfType<Sprite>().ToArray();
            if (sprites.Length == 0)
                return ToolResult<SpriteInfoResult>.Fail(
                    $"No sprites found at '{p.AssetPath}'. Set TextureType=Sprite via " +
                    "texture/set-import-settings first.", ErrorCodes.NOT_FOUND);

            var entries = sprites.Select(s => new SpriteEntry
            {
                Name = s.name,
                Rect = new[] { s.rect.x, s.rect.y, s.rect.width, s.rect.height },
                Pivot = new[] { s.pivot.x, s.pivot.y },
                PixelsPerUnit = s.pixelsPerUnit,
                Border = new[] { s.border.x, s.border.y, s.border.z, s.border.w },
            }).ToArray();

            return ToolResult<SpriteInfoResult>.Ok(new SpriteInfoResult { Sprites = entries });
        }
    }
}
