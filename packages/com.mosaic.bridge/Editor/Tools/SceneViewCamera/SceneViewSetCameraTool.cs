using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.SceneViewCamera
{
    public static class SceneViewSetCameraTool
    {
        [MosaicTool("sceneview/set-camera",
                    "Sets the SceneView camera position, rotation, pivot, size, and/or projection mode. " +
                    "Position sets the actual camera world position (computes the correct pivot). " +
                    "Pivot sets the orbit center. Use one or the other — if both are provided, Position wins.",
                    isReadOnly: false)]
        public static ToolResult<SceneViewSetCameraResult> SetCamera(SceneViewSetCameraParams p)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
                return ToolResult<SceneViewSetCameraResult>.Fail(
                    "No active SceneView found. Open a Scene window in the Unity Editor.",
                    ErrorCodes.NOT_FOUND);

            // Apply rotation first — it affects the pivot computation from Position
            if (p.Rotation != null && p.Rotation.Length == 3)
                sv.rotation = Quaternion.Euler(p.Rotation[0], p.Rotation[1], p.Rotation[2]);

            if (p.Size.HasValue)
                sv.size = p.Size.Value;

            if (p.Orthographic.HasValue)
                sv.orthographic = p.Orthographic.Value;

            if (p.Position != null && p.Position.Length == 3)
            {
                // In Unity SceneView: cameraPosition = pivot + rotation * (0, 0, -size)
                // So: pivot = cameraPosition - rotation * (0, 0, -size)
                var desiredPos = new Vector3(p.Position[0], p.Position[1], p.Position[2]);
                var offset = sv.rotation * new Vector3(0, 0, -sv.size);
                sv.pivot = desiredPos - offset;
            }
            else if (p.Pivot != null && p.Pivot.Length == 3)
            {
                // Only set pivot directly if Position wasn't provided
                sv.pivot = new Vector3(p.Pivot[0], p.Pivot[1], p.Pivot[2]);
            }

            sv.Repaint();

            var cam = sv.camera;
            var finalPos = cam.transform.position;
            var finalRot = cam.transform.eulerAngles;
            var finalPivot = sv.pivot;

            return ToolResult<SceneViewSetCameraResult>.Ok(new SceneViewSetCameraResult
            {
                Position     = new[] { finalPos.x, finalPos.y, finalPos.z },
                Rotation     = new[] { finalRot.x, finalRot.y, finalRot.z },
                Pivot        = new[] { finalPivot.x, finalPivot.y, finalPivot.z },
                Size         = sv.size,
                Orthographic = sv.orthographic
            });
        }
    }
}
