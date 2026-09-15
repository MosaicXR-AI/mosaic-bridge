using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainHeightmapTool
    {
        [MosaicTool("terrain/heightmap",
                    "import-texture: reads a grayscale Texture2D (Read/Write Enabled, exactly " +
                    "heightmapResolution²) into the terrain's heights. import-raw/export-raw: " +
                    "16-bit raw heightmap file (ByteOrder Little/Big) — real-world DEM terrains and " +
                    "hand-painted heightmaps; the Terrain Tools Toolbox equivalent is GUI-only. " +
                    "get-heights: flat normalized heights array.",
                    isReadOnly: false)]
        public static ToolResult<TerrainHeightmapResult> Execute(TerrainHeightmapParams p)
        {
            var terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.Name, out string error);
            if (terrain == null)
                return ToolResult<TerrainHeightmapResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var data = terrain.terrainData;
            int res = data.heightmapResolution;

            switch (p.Action?.ToLowerInvariant())
            {
                case "import-texture": return ImportTexture(terrain, data, res, p);
                case "import-raw":      return ImportRaw(terrain, data, res, p);
                case "export-raw":      return ExportRaw(terrain, data, res, p);
                case "get-heights":     return GetHeightsAction(terrain, data, res, p);
                default:
                    return ToolResult<TerrainHeightmapResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: import-texture, import-raw, export-raw, get-heights",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<TerrainHeightmapResult> ImportTexture(
            UnityEngine.Terrain terrain, TerrainData data, int res, TerrainHeightmapParams p)
        {
            if (string.IsNullOrEmpty(p.TexturePath))
                return ToolResult<TerrainHeightmapResult>.Fail("TexturePath is required for import-texture", ErrorCodes.INVALID_PARAM);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(p.TexturePath);
            if (texture == null)
                return ToolResult<TerrainHeightmapResult>.Fail($"Texture not found at '{p.TexturePath}'", ErrorCodes.NOT_FOUND);
            if (texture.width != res || texture.height != res)
                return ToolResult<TerrainHeightmapResult>.Fail(
                    $"Texture is {texture.width}x{texture.height} but heightmapResolution is {res}x{res} — no resampling is performed",
                    ErrorCodes.INVALID_PARAM);

            Color[] pixels;
            try
            {
                pixels = texture.GetPixels();
            }
            catch (UnityException)
            {
                return ToolResult<TerrainHeightmapResult>.Fail(
                    $"Texture at '{p.TexturePath}' is not Read/Write Enabled — enable it in the texture's import settings first",
                    ErrorCodes.INVALID_PARAM);
            }

            var heights = new float[res, res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    heights[y, x] = pixels[y * res + x].grayscale;

            data.SetHeights(0, 0, heights);
            Flush(terrain, data, p.DelayLod);

            return ToolResult<TerrainHeightmapResult>.Ok(new TerrainHeightmapResult
            {
                Action = "import-texture", InstanceId = UnityIds.Of(terrain.gameObject), Name = terrain.gameObject.name,
                Resolution = res, Message = $"Imported heights from '{p.TexturePath}' ({res}x{res})",
            });
        }

        private static ToolResult<TerrainHeightmapResult> ImportRaw(
            UnityEngine.Terrain terrain, TerrainData data, int res, TerrainHeightmapParams p)
        {
            if (!TryResolveRawPath(p.RawPath, out var fullPath, out var pathError))
                return ToolResult<TerrainHeightmapResult>.Fail(pathError, ErrorCodes.INVALID_PARAM);
            if (!File.Exists(fullPath))
                return ToolResult<TerrainHeightmapResult>.Fail($"Raw heightmap file not found at '{p.RawPath}'", ErrorCodes.NOT_FOUND);
            if (!TryParseByteOrder(p.ByteOrder, out var bigEndian))
                return ToolResult<TerrainHeightmapResult>.Fail(
                    $"Unknown ByteOrder '{p.ByteOrder}'. Valid: Little, Big", ErrorCodes.INVALID_PARAM);

            var bytes = File.ReadAllBytes(fullPath);
            long expected = (long)res * res * 2;
            if (bytes.LongLength != expected)
                return ToolResult<TerrainHeightmapResult>.Fail(
                    $"Raw file is {bytes.LongLength} bytes but heightmapResolution {res}x{res} needs exactly " +
                    $"{expected} bytes (2 bytes/sample) — no resampling is performed", ErrorCodes.INVALID_PARAM);

            var heights = new float[res, res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i = (y * res + x) * 2;
                    ushort sample = bigEndian
                        ? (ushort)((bytes[i] << 8) | bytes[i + 1])
                        : (ushort)(bytes[i] | (bytes[i + 1] << 8));
                    heights[y, x] = sample / 65535f;
                }
            }

            data.SetHeights(0, 0, heights);
            Flush(terrain, data, p.DelayLod);

            return ToolResult<TerrainHeightmapResult>.Ok(new TerrainHeightmapResult
            {
                Action = "import-raw", InstanceId = UnityIds.Of(terrain.gameObject), Name = terrain.gameObject.name,
                Resolution = res, Message = $"Imported {res}x{res} raw heightmap from '{p.RawPath}'",
            });
        }

        private static ToolResult<TerrainHeightmapResult> ExportRaw(
            UnityEngine.Terrain terrain, TerrainData data, int res, TerrainHeightmapParams p)
        {
            if (!TryResolveRawPath(p.RawPath, out var fullPath, out var pathError))
                return ToolResult<TerrainHeightmapResult>.Fail(pathError, ErrorCodes.INVALID_PARAM);
            if (!TryParseByteOrder(p.ByteOrder, out var bigEndian))
                return ToolResult<TerrainHeightmapResult>.Fail(
                    $"Unknown ByteOrder '{p.ByteOrder}'. Valid: Little, Big", ErrorCodes.INVALID_PARAM);

            var heights = data.GetHeights(0, 0, res, res);
            var bytes = new byte[(long)res * res * 2];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    ushort sample = (ushort)Mathf.RoundToInt(Mathf.Clamp01(heights[y, x]) * 65535f);
                    int i = (y * res + x) * 2;
                    if (bigEndian)
                    {
                        bytes[i] = (byte)(sample >> 8);
                        bytes[i + 1] = (byte)(sample & 0xFF);
                    }
                    else
                    {
                        bytes[i] = (byte)(sample & 0xFF);
                        bytes[i + 1] = (byte)(sample >> 8);
                    }
                }
            }

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(fullPath, bytes);

            return ToolResult<TerrainHeightmapResult>.Ok(new TerrainHeightmapResult
            {
                Action = "export-raw", InstanceId = UnityIds.Of(terrain.gameObject), Name = terrain.gameObject.name,
                Resolution = res, Message = $"Exported {res}x{res} raw heightmap to '{p.RawPath}'",
            });
        }

        private static ToolResult<TerrainHeightmapResult> GetHeightsAction(
            UnityEngine.Terrain terrain, TerrainData data, int res, TerrainHeightmapParams p)
        {
            var heights2D = data.GetHeights(0, 0, res, res);
            var flat = new float[res * res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    flat[y * res + x] = heights2D[y, x];

            return ToolResult<TerrainHeightmapResult>.Ok(new TerrainHeightmapResult
            {
                Action = "get-heights", InstanceId = UnityIds.Of(terrain.gameObject), Name = terrain.gameObject.name,
                Resolution = res, Heights = flat, Message = $"Read {res}x{res} heights",
            });
        }

        private static void Flush(UnityEngine.Terrain terrain, TerrainData data, bool delayLod)
        {
            if (delayLod) return;
            data.SyncHeightmap();
            terrain.Flush();
        }

        private static bool TryResolveRawPath(string rawPath, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;
            if (string.IsNullOrEmpty(rawPath))
            {
                error = "RawPath is required";
                return false;
            }
            fullPath = Path.IsPathRooted(rawPath)
                ? rawPath
                : Path.Combine(Application.dataPath, "..", rawPath);
            return true;
        }

        private static bool TryParseByteOrder(string value, out bool bigEndian)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "little": bigEndian = false; return true;
                case "big":    bigEndian = true;  return true;
                default:       bigEndian = false; return false;
            }
        }
    }
}
