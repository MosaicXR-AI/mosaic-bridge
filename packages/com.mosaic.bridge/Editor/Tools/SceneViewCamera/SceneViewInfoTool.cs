using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.SceneViewCamera
{
    public static class SceneViewInfoTool
    {
        [MosaicTool("sceneview/info",
                    "Queries the last active SceneView camera position, rotation, FOV, and projection mode",
                    isReadOnly: true)]
        public static ToolResult<SceneViewInfoResult> Info(SceneViewInfoParams p)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
                return ToolResult<SceneViewInfoResult>.Fail(
                    "No active SceneView found. Open a Scene window in the Unity Editor.",
                    ErrorCodes.NOT_FOUND);

            var cam = sv.camera;
            var pos = cam.transform.position;
            var rot = cam.transform.eulerAngles;
            var pivot = sv.pivot;

            return ToolResult<SceneViewInfoResult>.Ok(new SceneViewInfoResult
            {
                Position     = new[] { pos.x, pos.y, pos.z },
                Rotation     = new[] { rot.x, rot.y, rot.z },
                Pivot        = new[] { pivot.x, pivot.y, pivot.z },
                Size         = sv.size,
                FieldOfView  = cam.fieldOfView,
                Orthographic = sv.orthographic
            });
        }
    }
}
