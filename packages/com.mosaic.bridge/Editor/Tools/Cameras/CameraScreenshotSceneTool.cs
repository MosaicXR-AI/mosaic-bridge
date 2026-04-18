using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cameras
{
    public static class CameraScreenshotSceneTool
    {
        [MosaicTool("camera/screenshot-scene",
                    "Captures a screenshot from the active Scene View camera. Saves to file and returns the path. " +
                    "Set IncludeBase64=true to also embed image data in the response. " +
                    "Supports PNG (lossless, default) and JPEG (smaller) format.",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<CameraScreenshotSceneResult> Execute(CameraScreenshotSceneParams p)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
                return ToolResult<CameraScreenshotSceneResult>.Fail(
                    "No active Scene View found. Open a Scene View window first.",
                    ErrorCodes.NOT_FOUND);

            var (w, h) = CameraToolHelpers.ResolveResolution(p.Width, p.Height);
            var capture = CameraToolHelpers.Capture(sceneView.camera, w, h, p.Format, p.Quality, p.SavePath, p.IncludeBase64);

            return ToolResult<CameraScreenshotSceneResult>.Ok(new CameraScreenshotSceneResult
            {
                FilePath  = capture.FilePath,
                Base64Png = capture.Base64,
                Format    = capture.Format,
                ByteSize  = capture.ByteSize,
                Width     = w,
                Height    = h
            });
        }
    }
}
