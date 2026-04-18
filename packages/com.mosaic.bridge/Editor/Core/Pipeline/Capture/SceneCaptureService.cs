using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mosaic.Bridge.Core.Pipeline.Capture
{
    /// <summary>
    /// Captures screenshots of a target GameObject from multiple camera angles.
    /// Must be called on the Unity main thread (uses rendering APIs).
    /// </summary>
    public sealed class SceneCaptureService
    {
        /// <summary>
        /// Renders the target from each angle defined in <paramref name="settings"/>
        /// and returns a list of base-64 encoded PNG screenshots.
        /// </summary>
        public List<ScreenshotData> CaptureAroundTarget(GameObject target, CaptureSettings settings)
        {
            var screenshots = new List<ScreenshotData>();
            if (target == null) return screenshots;

            var bounds   = ComputeBounds(target);
            var center   = bounds.center;
            var distance = Mathf.Max(bounds.extents.magnitude * 2.5f, 2f);

            var cameraGo = new GameObject("__MosaicCapture");
            cameraGo.hideFlags = HideFlags.HideAndDontSave;
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags       = CameraClearFlags.SolidColor;
            camera.backgroundColor  = new Color(0.2f, 0.2f, 0.2f, 1f);
            camera.nearClipPlane    = 0.01f;
            camera.farClipPlane     = distance * 10f;

            var rt = RenderTexture.GetTemporary(settings.Resolution, settings.Resolution, 24);
            camera.targetTexture = rt;

            try
            {
                foreach (var angleName in settings.Angles)
                {
                    var (direction, up) = CameraAngle.GetAngle(angleName);

                    camera.transform.position = center + direction * distance;
                    camera.transform.LookAt(center, up);

                    bool isOrtho = angleName == "front" || angleName == "right" ||
                                   angleName == "left"  || angleName == "top"   ||
                                   angleName == "back";
                    camera.orthographic = isOrtho;
                    if (isOrtho)
                        camera.orthographicSize = bounds.extents.magnitude * 1.2f;
                    else
                        camera.fieldOfView = 60f;

                    camera.Render();

                    var prevRT = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(settings.Resolution, settings.Resolution, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, settings.Resolution, settings.Resolution), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prevRT;

                    var pngBytes = tex.EncodeToPNG();
                    UnityEngine.Object.DestroyImmediate(tex);

                    screenshots.Add(new ScreenshotData
                    {
                        AngleLabel = angleName,
                        Base64Png  = Convert.ToBase64String(pngBytes),
                        Width      = settings.Resolution,
                        Height     = settings.Resolution
                    });
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(cameraGo);
            }

            return screenshots;
        }

        private static Bounds ComputeBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(target.transform.position, Vector3.one * 2f);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
