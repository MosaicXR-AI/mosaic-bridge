using System;
using System.IO;
using UnityEngine;

namespace Mosaic.Bridge.Tools.Cameras
{
    /// <summary>
    /// Shared helper methods for camera/screenshot tools.
    /// Encapsulates RenderTexture capture, file saving, and cleanup.
    /// </summary>
    internal static class CameraToolHelpers
    {
        public const int DefaultWidth = 1920;
        public const int DefaultHeight = 1080;
        public const int DefaultJpegQuality = 75;

        private static readonly string ScreenshotDir = Path.Combine(
            Application.persistentDataPath, "MosaicBridge", "Screenshots");

        public struct CaptureResult
        {
            public string FilePath;
            public string Base64;
            public string Format;
            public int ByteSize;
        }

        /// <summary>
        /// Renders the given camera and saves to file. Returns file path (always)
        /// and base64 (only if includeBase64 is true).
        /// </summary>
        public static CaptureResult Capture(UnityEngine.Camera camera, int width, int height,
            string format = null, int? quality = null, string savePath = null, bool includeBase64 = false)
        {
            RenderTexture rt = null;
            Texture2D tex = null;
            RenderTexture prevActive = null;
            RenderTexture prevTarget = camera.targetTexture;

            try
            {
                rt = RenderTexture.GetTemporary(width, height, 24);
                camera.targetTexture = rt;
                camera.Render();

                prevActive = RenderTexture.active;
                RenderTexture.active = rt;

                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                bool useJpeg = !string.IsNullOrEmpty(format) &&
                               format.Equals("jpeg", StringComparison.OrdinalIgnoreCase);

                byte[] bytes;
                string resolvedFormat;
                string extension;

                if (useJpeg)
                {
                    int q = quality.HasValue ? Mathf.Clamp(quality.Value, 1, 100) : DefaultJpegQuality;
                    bytes = tex.EncodeToJPG(q);
                    resolvedFormat = "jpeg";
                    extension = ".jpg";
                }
                else
                {
                    bytes = tex.EncodeToPNG();
                    resolvedFormat = "png";
                    extension = ".png";
                }

                // Determine file path
                string filePath;
                if (!string.IsNullOrEmpty(savePath))
                {
                    filePath = savePath;
                }
                else
                {
                    Directory.CreateDirectory(ScreenshotDir);
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
                    filePath = Path.Combine(ScreenshotDir, $"screenshot_{timestamp}{extension}");
                }

                // Ensure parent directory exists
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(filePath, bytes);

                return new CaptureResult
                {
                    FilePath = filePath,
                    Base64 = includeBase64 ? Convert.ToBase64String(bytes) : null,
                    Format = resolvedFormat,
                    ByteSize = bytes.Length
                };
            }
            finally
            {
                RenderTexture.active = prevActive;
                camera.targetTexture = prevTarget;

                if (tex != null)
                    UnityEngine.Object.DestroyImmediate(tex);

                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>
        /// Legacy wrapper for backward compatibility — returns base64 PNG string.
        /// </summary>
        public static string CaptureToBase64Png(UnityEngine.Camera camera, int width, int height)
        {
            return Capture(camera, width, height, "png", null, null, includeBase64: true).Base64;
        }

        /// <summary>
        /// Resolves width/height from nullable params, applying defaults.
        /// Clamps to a maximum of 4096 to prevent excessive memory usage.
        /// </summary>
        public static (int width, int height) ResolveResolution(int? width, int? height)
        {
            int w = width.HasValue && width.Value > 0 ? Mathf.Min(width.Value, 4096) : DefaultWidth;
            int h = height.HasValue && height.Value > 0 ? Mathf.Min(height.Value, 4096) : DefaultHeight;
            return (w, h);
        }
    }
}
