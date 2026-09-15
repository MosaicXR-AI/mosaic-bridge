#if MOSAIC_HAS_CINEMACHINE
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineAddExtensionTool
    {
        [MosaicTool("cinemachine/add-extension",
                    "Adds a Cinemachine extension component to a vcam. ExtensionType: Confiner2D " +
                    "(ConfinerBoundingShapeName + ConfinerDamping — 2D camera confined to level bounds), " +
                    "Confiner3D (ConfinerBoundingVolumeName), Deoccluder (DeoccluderCollideAgainst, " +
                    "comma-separated layer names), Decollider (DecolliderCameraRadius), " +
                    "ImpulseListener (ImpulseChannelMask/Gain), FreeLookModifier (FreeLookEasing — " +
                    "needs an OrbitalFollow body), Storyboard (StoryboardImagePath/Alpha/MuteCamera), " +
                    "FollowZoom (FollowZoomWidth/FovMin/FovMax/Damping), CameraOffset " +
                    "(CameraOffsetValue [x,y,z]), PixelPerfect (no params), Recomposer " +
                    "(RecomposerZoomScale/Tilt/Pan/Dutch).",
                    isReadOnly: false)]
        public static ToolResult<CinemachineAddExtensionResult> Execute(CinemachineAddExtensionParams p)
        {
            var go = GameObject.Find(p.VCamName);
            if (go == null)
                return ToolResult<CinemachineAddExtensionResult>.Fail(
                    $"GameObject '{p.VCamName}' not found", ErrorCodes.NOT_FOUND);

            var vcam = go.GetComponent<CinemachineCamera>();
            if (vcam == null)
                return ToolResult<CinemachineAddExtensionResult>.Fail(
                    $"GameObject '{p.VCamName}' does not have a CinemachineCamera component",
                    ErrorCodes.INVALID_PARAM);

            switch (p.ExtensionType?.ToLowerInvariant())
            {
                case "confiner2d":
                {
                    if (string.IsNullOrEmpty(p.ConfinerBoundingShapeName))
                        return Fail("ConfinerBoundingShapeName is required for Confiner2D");
                    var shapeGo = GameObject.Find(p.ConfinerBoundingShapeName);
                    if (shapeGo == null)
                        return Fail($"GameObject '{p.ConfinerBoundingShapeName}' not found", ErrorCodes.NOT_FOUND);
                    var collider2D = shapeGo.GetComponent<Collider2D>();
                    if (collider2D == null)
                        return Fail($"'{p.ConfinerBoundingShapeName}' has no Collider2D component", ErrorCodes.NOT_FOUND);
                    var confiner2D = go.AddComponent<CinemachineConfiner2D>();
                    confiner2D.BoundingShape2D = collider2D;
                    if (p.ConfinerDamping.HasValue) confiner2D.Damping = p.ConfinerDamping.Value;
                    confiner2D.InvalidateBoundingShapeCache();
                    break;
                }
                case "confiner3d":
                {
                    if (string.IsNullOrEmpty(p.ConfinerBoundingVolumeName))
                        return Fail("ConfinerBoundingVolumeName is required for Confiner3D");
                    var volumeGo = GameObject.Find(p.ConfinerBoundingVolumeName);
                    if (volumeGo == null)
                        return Fail($"GameObject '{p.ConfinerBoundingVolumeName}' not found", ErrorCodes.NOT_FOUND);
                    var collider3D = volumeGo.GetComponent<Collider>();
                    if (collider3D == null)
                        return Fail($"'{p.ConfinerBoundingVolumeName}' has no Collider component", ErrorCodes.NOT_FOUND);
                    var confiner3D = go.AddComponent<CinemachineConfiner3D>();
                    confiner3D.BoundingVolume = collider3D;
                    break;
                }
                case "deoccluder":
                {
                    var deoccluder = go.AddComponent<CinemachineDeoccluder>();
                    if (!string.IsNullOrEmpty(p.DeoccluderCollideAgainst))
                        deoccluder.CollideAgainst = LayerMask.GetMask(
                            System.Array.ConvertAll(p.DeoccluderCollideAgainst.Split(','), s => s.Trim()));
                    break;
                }
                case "decollider":
                {
                    var decollider = go.AddComponent<CinemachineDecollider>();
                    if (p.DecolliderCameraRadius.HasValue) decollider.CameraRadius = p.DecolliderCameraRadius.Value;
                    break;
                }
                case "impulselistener":
                {
                    var impulseListener = go.AddComponent<CinemachineImpulseListener>();
                    if (p.ImpulseChannelMask.HasValue) impulseListener.ChannelMask = p.ImpulseChannelMask.Value;
                    if (p.ImpulseGain.HasValue) impulseListener.Gain = p.ImpulseGain.Value;
                    break;
                }
                case "freelookmodifier":
                {
                    var freeLookModifier = go.AddComponent<CinemachineFreeLookModifier>();
                    if (p.FreeLookEasing.HasValue) freeLookModifier.Easing = p.FreeLookEasing.Value;
                    break;
                }
                case "storyboard":
                {
                    var storyboard = go.AddComponent<CinemachineStoryboard>();
                    if (!string.IsNullOrEmpty(p.StoryboardImagePath))
                    {
                        var texture = AssetDatabase.LoadAssetAtPath<Texture>(p.StoryboardImagePath);
                        if (texture == null)
                            return Fail($"No Texture found at '{p.StoryboardImagePath}'", ErrorCodes.NOT_FOUND);
                        storyboard.Image = texture;
                        storyboard.ShowImage = true;
                    }
                    if (p.StoryboardAlpha.HasValue) storyboard.Alpha = p.StoryboardAlpha.Value;
                    if (p.StoryboardMuteCamera.HasValue) storyboard.MuteCamera = p.StoryboardMuteCamera.Value;
                    break;
                }
                case "followzoom":
                {
                    var followZoom = go.AddComponent<CinemachineFollowZoom>();
                    if (p.FollowZoomWidth.HasValue) followZoom.Width = p.FollowZoomWidth.Value;
                    if (p.FollowZoomFovMin.HasValue || p.FollowZoomFovMax.HasValue)
                    {
                        var range = followZoom.FovRange;
                        if (p.FollowZoomFovMin.HasValue) range.x = p.FollowZoomFovMin.Value;
                        if (p.FollowZoomFovMax.HasValue) range.y = p.FollowZoomFovMax.Value;
                        followZoom.FovRange = range;
                    }
                    if (p.FollowZoomDamping.HasValue) followZoom.Damping = p.FollowZoomDamping.Value;
                    break;
                }
                case "cameraoffset":
                {
                    var cameraOffset = go.AddComponent<CinemachineCameraOffset>();
                    if (p.CameraOffsetValue != null)
                    {
                        if (p.CameraOffsetValue.Length != 3)
                            return Fail("CameraOffsetValue requires exactly [x, y, z]");
                        cameraOffset.Offset = new Vector3(p.CameraOffsetValue[0], p.CameraOffsetValue[1], p.CameraOffsetValue[2]);
                    }
                    break;
                }
                case "pixelperfect":
                    go.AddComponent<CinemachinePixelPerfect>();
                    break;
                case "recomposer":
                {
                    var recomposer = go.AddComponent<CinemachineRecomposer>();
                    if (p.RecomposerZoomScale.HasValue) recomposer.ZoomScale = p.RecomposerZoomScale.Value;
                    if (p.RecomposerTilt.HasValue) recomposer.Tilt = p.RecomposerTilt.Value;
                    if (p.RecomposerPan.HasValue) recomposer.Pan = p.RecomposerPan.Value;
                    if (p.RecomposerDutch.HasValue) recomposer.Dutch = p.RecomposerDutch.Value;
                    break;
                }
                default:
                    return ToolResult<CinemachineAddExtensionResult>.Fail(
                        $"Unknown ExtensionType '{p.ExtensionType}'. Valid: Confiner2D, Confiner3D, " +
                        "Deoccluder, Decollider, ImpulseListener, FreeLookModifier, Storyboard, " +
                        "FollowZoom, CameraOffset, PixelPerfect, Recomposer", ErrorCodes.INVALID_PARAM);
            }

            EditorUtility.SetDirty(go);

            return ToolResult<CinemachineAddExtensionResult>.Ok(new CinemachineAddExtensionResult
            {
                VCamName = go.name,
                ExtensionType = p.ExtensionType,
            });
        }

        private static ToolResult<CinemachineAddExtensionResult> Fail(string message, string code = ErrorCodes.INVALID_PARAM) =>
            ToolResult<CinemachineAddExtensionResult>.Fail(message, code);
    }
}
#endif
