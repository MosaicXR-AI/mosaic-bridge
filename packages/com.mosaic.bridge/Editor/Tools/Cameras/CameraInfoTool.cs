using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cameras
{
    public static class CameraInfoTool
    {
        [MosaicTool("camera/info",
                    "Returns information about scene cameras (FOV, clips, clearFlags, cullingMask, depth, position, rotation)",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<CameraInfoResult> Execute(CameraInfoParams p)
        {
            if (p.InstanceId.HasValue)
            {
                var obj = UnityEngine.Resources.EntityIdToObject(p.InstanceId.Value);
                if (obj == null)
                    return ToolResult<CameraInfoResult>.Fail(
                        $"No object found with InstanceId {p.InstanceId.Value}",
                        ErrorCodes.NOT_FOUND);

                UnityEngine.Camera camera;

                if (obj is GameObject go)
                    camera = go.GetComponent<UnityEngine.Camera>();
                else if (obj is UnityEngine.Camera cam)
                    camera = cam;
                else
                    return ToolResult<CameraInfoResult>.Fail(
                        $"Object with InstanceId {p.InstanceId.Value} is not a Camera or GameObject with a Camera component",
                        ErrorCodes.INVALID_PARAM);

                if (camera == null)
                    return ToolResult<CameraInfoResult>.Fail(
                        $"GameObject with InstanceId {p.InstanceId.Value} does not have a Camera component",
                        ErrorCodes.NOT_FOUND);

                return ToolResult<CameraInfoResult>.Ok(new CameraInfoResult
                {
                    Cameras = new[] { BuildEntry(camera) }
                });
            }

            // Return all cameras in the scene
            var allCameras = UnityEngine.Camera.allCameras;
            if (allCameras.Length == 0)
                return ToolResult<CameraInfoResult>.Fail(
                    "No cameras found in the current scene.",
                    ErrorCodes.NOT_FOUND);

            var entries = new List<CameraInfoEntry>(allCameras.Length);
            foreach (var cam in allCameras)
                entries.Add(BuildEntry(cam));

            return ToolResult<CameraInfoResult>.Ok(new CameraInfoResult
            {
                Cameras = entries.ToArray()
            });
        }

        private static CameraInfoEntry BuildEntry(UnityEngine.Camera camera)
        {
            var t = camera.transform;
            var pos = t.position;
            var rot = t.eulerAngles;

            return new CameraInfoEntry
            {
                InstanceId      = camera.GetInstanceID(),
                Name            = camera.gameObject.name,
                HierarchyPath   = GetHierarchyPath(t),
                FieldOfView     = camera.fieldOfView,
                NearClipPlane   = camera.nearClipPlane,
                FarClipPlane    = camera.farClipPlane,
                ClearFlags      = camera.clearFlags.ToString(),
                CullingMask     = camera.cullingMask,
                Depth           = camera.depth,
                IsOrthographic  = camera.orthographic,
                OrthographicSize = camera.orthographicSize,
                Position        = new[] { pos.x, pos.y, pos.z },
                Rotation        = new[] { rot.x, rot.y, rot.z },
                IsMainCamera    = camera == UnityEngine.Camera.main
            };
        }

        private static string GetHierarchyPath(Transform t)
        {
            var path = t.name;
            var current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}
