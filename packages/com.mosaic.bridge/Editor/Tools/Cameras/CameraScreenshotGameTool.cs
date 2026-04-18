using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cameras
{
    public static class CameraScreenshotGameTool
    {
        [MosaicTool("camera/screenshot-game",
                    "Captures a screenshot from the Game View. Saves to file and returns the path. " +
                    "Set IncludeBase64=true to also embed image data in the response. " +
                    "Supports PNG (lossless, default) and JPEG (smaller) format.",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<CameraScreenshotGameResult> Execute(CameraScreenshotGameParams p)
        {
            UnityEngine.Camera camera;

            if (p.CameraInstanceId.HasValue)
            {
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(p.CameraInstanceId.Value);
                if (obj == null)
                    return ToolResult<CameraScreenshotGameResult>.Fail(
                        $"No object found with InstanceId {p.CameraInstanceId.Value}",
                        ErrorCodes.NOT_FOUND);

                if (obj is GameObject go)
                    camera = go.GetComponent<UnityEngine.Camera>();
                else if (obj is UnityEngine.Camera cam)
                    camera = cam;
                else
                    return ToolResult<CameraScreenshotGameResult>.Fail(
                        $"Object with InstanceId {p.CameraInstanceId.Value} is not a Camera or GameObject with a Camera component",
                        ErrorCodes.INVALID_PARAM);

                if (camera == null)
                    return ToolResult<CameraScreenshotGameResult>.Fail(
                        $"GameObject with InstanceId {p.CameraInstanceId.Value} does not have a Camera component",
                        ErrorCodes.NOT_FOUND);
            }
            else
            {
                camera = UnityEngine.Camera.main;
                if (camera == null)
                    return ToolResult<CameraScreenshotGameResult>.Fail(
                        "No main camera found. Tag a camera as 'MainCamera' or provide a CameraInstanceId.",
                        ErrorCodes.NOT_FOUND);
            }

            var (w, h) = CameraToolHelpers.ResolveResolution(p.Width, p.Height);
            var capture = CameraToolHelpers.Capture(camera, w, h, p.Format, p.Quality, p.SavePath, p.IncludeBase64);

            return ToolResult<CameraScreenshotGameResult>.Ok(new CameraScreenshotGameResult
            {
                FilePath         = capture.FilePath,
                Base64Png        = capture.Base64,
                Format           = capture.Format,
                ByteSize         = capture.ByteSize,
                Width            = w,
                Height           = h,
                CameraInstanceId = camera.GetInstanceID(),
                CameraName       = camera.gameObject.name
            });
        }
    }
}
