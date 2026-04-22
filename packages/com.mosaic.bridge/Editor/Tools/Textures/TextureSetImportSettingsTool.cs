using System;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Textures
{
    public static class TextureSetImportSettingsTool
    {
        [MosaicTool("texture/set-import-settings",
                    "Sets texture import settings (type, shape, max size, compression, sRGB, filter mode, wrap mode). " +
                    "Use TextureShape=Cube with TextureType=Default to convert an equirectangular HDRI into a Cubemap " +
                    "(e.g. for skyboxes). Closes issue #7.",
                    isReadOnly: false)]
        public static ToolResult<TextureSetImportSettingsResult> SetImportSettings(TextureSetImportSettingsParams p)
        {
            var importer = AssetImporter.GetAtPath(p.AssetPath) as TextureImporter;
            if (importer == null)
                return ToolResult<TextureSetImportSettingsResult>.Fail(
                    $"No texture found at '{p.AssetPath}'. Ensure the path is a valid texture asset.",
                    ErrorCodes.NOT_FOUND);

            // Texture Type
            if (!string.IsNullOrEmpty(p.TextureType))
            {
                if (!TryParseTextureType(p.TextureType, out var textureType))
                    return ToolResult<TextureSetImportSettingsResult>.Fail(
                        $"Unknown TextureType '{p.TextureType}'. Valid: Default, NormalMap, Sprite, Editor",
                        ErrorCodes.INVALID_PARAM);
                importer.textureType = textureType;
            }

            // Max Size
            if (p.MaxSize.HasValue)
                importer.maxTextureSize = p.MaxSize.Value;

            // Compression
            if (!string.IsNullOrEmpty(p.Compression))
            {
                if (!TryParseCompression(p.Compression, out var compression))
                    return ToolResult<TextureSetImportSettingsResult>.Fail(
                        $"Unknown Compression '{p.Compression}'. Valid: None, LowQuality, NormalQuality, HighQuality",
                        ErrorCodes.INVALID_PARAM);
                importer.textureCompression = compression;
            }

            // sRGB
            if (p.SRGB.HasValue)
                importer.sRGBTexture = p.SRGB.Value;

            // Filter Mode
            if (!string.IsNullOrEmpty(p.FilterMode))
            {
                if (!TryParseFilterMode(p.FilterMode, out var filterMode))
                    return ToolResult<TextureSetImportSettingsResult>.Fail(
                        $"Unknown FilterMode '{p.FilterMode}'. Valid: Point, Bilinear, Trilinear",
                        ErrorCodes.INVALID_PARAM);
                importer.filterMode = filterMode;
            }

            // Wrap Mode
            if (!string.IsNullOrEmpty(p.WrapMode))
            {
                if (!TryParseWrapMode(p.WrapMode, out var wrapMode))
                    return ToolResult<TextureSetImportSettingsResult>.Fail(
                        $"Unknown WrapMode '{p.WrapMode}'. Valid: Repeat, Clamp, Mirror, MirrorOnce",
                        ErrorCodes.INVALID_PARAM);
                importer.wrapMode = wrapMode;
            }

            // Texture Shape (closes issue #7 — HDRI→Cubemap workflow)
            if (!string.IsNullOrEmpty(p.TextureShape))
            {
                if (!TryParseTextureShape(p.TextureShape, out var textureShape))
                    return ToolResult<TextureSetImportSettingsResult>.Fail(
                        $"Unknown TextureShape '{p.TextureShape}'. Valid: 2D, Cube, 2DArray, 3D",
                        ErrorCodes.INVALID_PARAM);
                importer.textureShape = textureShape;
            }

            importer.SaveAndReimport();

            return ToolResult<TextureSetImportSettingsResult>.Ok(new TextureSetImportSettingsResult
            {
                AssetPath    = p.AssetPath,
                TextureType  = importer.textureType.ToString(),
                TextureShape = importer.textureShape.ToString(),
                MaxSize      = importer.maxTextureSize,
                Compression  = importer.textureCompression.ToString(),
                SRGB         = importer.sRGBTexture,
                FilterMode   = importer.filterMode.ToString(),
                WrapMode     = importer.wrapMode.ToString()
            });
        }

        private static bool TryParseTextureType(string value, out TextureImporterType result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "default":   result = TextureImporterType.Default;   return true;
                case "normalmap": result = TextureImporterType.NormalMap; return true;
                case "sprite":    result = TextureImporterType.Sprite;    return true;
                case "editor":
                case "editorgui": result = TextureImporterType.GUI;       return true;
                default:          result = TextureImporterType.Default;   return false;
            }
        }

        private static bool TryParseCompression(string value, out TextureImporterCompression result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "none":          result = TextureImporterCompression.Uncompressed;    return true;
                case "lowquality":    result = TextureImporterCompression.CompressedLQ;    return true;
                case "normalquality": result = TextureImporterCompression.Compressed;      return true;
                case "highquality":   result = TextureImporterCompression.CompressedHQ;    return true;
                default:              result = TextureImporterCompression.Compressed;       return false;
            }
        }

        private static bool TryParseTextureShape(string value, out TextureImporterShape result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "2d":        result = TextureImporterShape.Texture2D;      return true;
                case "cube":
                case "cubemap":   result = TextureImporterShape.TextureCube;    return true;
                case "2darray":   result = TextureImporterShape.Texture2DArray; return true;
                case "3d":        result = TextureImporterShape.Texture3D;      return true;
                default:          result = TextureImporterShape.Texture2D;      return false;
            }
        }

        private static bool TryParseFilterMode(string value, out FilterMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "point":     result = FilterMode.Point;     return true;
                case "bilinear":  result = FilterMode.Bilinear;  return true;
                case "trilinear": result = FilterMode.Trilinear; return true;
                default:          result = FilterMode.Bilinear;  return false;
            }
        }

        private static bool TryParseWrapMode(string value, out TextureWrapMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "repeat":     result = TextureWrapMode.Repeat;     return true;
                case "clamp":      result = TextureWrapMode.Clamp;      return true;
                case "mirror":     result = TextureWrapMode.Mirror;     return true;
                case "mirroronce": result = TextureWrapMode.MirrorOnce; return true;
                default:           result = TextureWrapMode.Repeat;     return false;
            }
        }
    }
}
