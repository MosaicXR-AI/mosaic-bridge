using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cameras
{
    public static class CameraScreenshotCameraTool
    {
        [MosaicTool("camera/screenshot-camera",
                    "Captures a screenshot from a specific camera by InstanceId. Saves to file and returns the path. " +
                    "Set IncludeBase64=true to also embed image data in the response. " +
                    "Supports PNG (lossless, default) and JPEG (smaller) format.",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<CameraScreenshotCameraResult> Execute(CameraScreenshotCameraParams p)
        {
            var obj = UnityEditor.EditorUtility.InstanceIDToObject(p.InstanceId);
            if (obj == null)
                return ToolResult<CameraScreenshotCameraResult>.Fail(
                    $"No object found with InstanceId {p.InstanceId}",
                    ErrorCodes.NOT_FOUND);

            UnityEngine.Camera camera;

            if (obj is GameObject go)
                camera = go.GetComponent<UnityEngine.Camera>();
            else if (obj is UnityEngine.Camera cam)
                camera = cam;
            else
                return ToolResult<CameraScreenshotCameraResult>.Fail(
                    $"Object with InstanceId {p.InstanceId} is not a Camera or GameObject with a Camera component",
                    ErrorCodes.INVALID_PARAM);

            if (camera == null)
                return ToolResult<CameraScreenshotCameraResult>.Fail(
                    $"GameObject with InstanceId {p.InstanceId} does not have a Camera component",
                    ErrorCodes.NOT_FOUND);

            var (w, h) = CameraToolHelpers.ResolveResolution(p.Width, p.Height);
            var capture = CameraToolHelpers.Capture(camera, w, h, p.Format, p.Quality, p.SavePath, p.IncludeBase64);

            return ToolResult<CameraScreenshotCameraResult>.Ok(new CameraScreenshotCameraResult
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
