#if MOSAIC_HAS_CINEMACHINE
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineSetPropertiesTool
    {
        [MosaicTool("cinemachine/set-properties",
                    "Configures virtual camera properties such as priority, FOV, follow offset, damping, and targets",
                    isReadOnly: false)]
        public static ToolResult<CinemachineSetPropertiesResult> Execute(CinemachineSetPropertiesParams p)
        {
            var go = GameObject.Find(p.VCamName);
            if (go == null)
                return ToolResult<CinemachineSetPropertiesResult>.Fail(
                    $"GameObject '{p.VCamName}' not found", ErrorCodes.NOT_FOUND);

            var vcam = go.GetComponent<CinemachineCamera>();
            if (vcam == null)
                return ToolResult<CinemachineSetPropertiesResult>.Fail(
                    $"GameObject '{p.VCamName}' does not have a CinemachineCamera component",
                    ErrorCodes.INVALID_PARAM);

            Undo.RecordObject(vcam, "Mosaic: Cinemachine Set Properties");

            var propsSet = new List<string>();

            if (p.Priority.HasValue)
            {
                vcam.Priority.Value = p.Priority.Value;
                propsSet.Add("Priority");
            }

            if (p.FieldOfView.HasValue)
            {
                vcam.Lens = new LensSettings
                {
                    FieldOfView = p.FieldOfView.Value,
                    NearClipPlane = p.NearClip ?? vcam.Lens.NearClipPlane,
                    FarClipPlane = p.FarClip ?? vcam.Lens.FarClipPlane,
                    OrthographicSize = vcam.Lens.OrthographicSize
                };
                propsSet.Add("FieldOfView");
                if (p.NearClip.HasValue) propsSet.Add("NearClip");
                if (p.FarClip.HasValue) propsSet.Add("FarClip");
            }
            else
            {
                if (p.NearClip.HasValue || p.FarClip.HasValue)
                {
                    vcam.Lens = new LensSettings
                    {
                        FieldOfView = vcam.Lens.FieldOfView,
                        NearClipPlane = p.NearClip ?? vcam.Lens.NearClipPlane,
                        FarClipPlane = p.FarClip ?? vcam.Lens.FarClipPlane,
                        OrthographicSize = vcam.Lens.OrthographicSize
                    };
                    if (p.NearClip.HasValue) propsSet.Add("NearClip");
                    if (p.FarClip.HasValue) propsSet.Add("FarClip");
                }
            }

            if (!string.IsNullOrEmpty(p.FollowTarget))
            {
                var followGo = GameObject.Find(p.FollowTarget);
                if (followGo == null)
                    return ToolResult<CinemachineSetPropertiesResult>.Fail(
                        $"Follow target '{p.FollowTarget}' not found", ErrorCodes.NOT_FOUND);
                vcam.Follow = followGo.transform;
                propsSet.Add("FollowTarget");
            }

            if (!string.IsNullOrEmpty(p.LookAtTarget))
            {
                var lookAtGo = GameObject.Find(p.LookAtTarget);
                if (lookAtGo == null)
                    return ToolResult<CinemachineSetPropertiesResult>.Fail(
                        $"LookAt target '{p.LookAtTarget}' not found", ErrorCodes.NOT_FOUND);
                vcam.LookAt = lookAtGo.transform;
                propsSet.Add("LookAtTarget");
            }

            // Apply follow offset and damping via SerializedObject for body components
            if (p.FollowOffset != null && p.FollowOffset.Length == 3)
            {
                var thirdPerson = go.GetComponent<CinemachineThirdPersonFollow>();
                if (thirdPerson != null)
                {
                    Undo.RecordObject(thirdPerson, "Mosaic: Cinemachine Set FollowOffset");
                    var so = new SerializedObject(thirdPerson);
                    var offsetProp = so.FindProperty("ShoulderOffset");
                    if (offsetProp != null)
                    {
                        offsetProp.vector3Value = new Vector3(
                            p.FollowOffset[0], p.FollowOffset[1], p.FollowOffset[2]);
                        so.ApplyModifiedProperties();
                    }
                    propsSet.Add("FollowOffset");
                }
                else
                {
                    var posComposer = go.GetComponent<CinemachinePositionComposer>();
                    if (posComposer != null)
                    {
                        Undo.RecordObject(posComposer, "Mosaic: Cinemachine Set FollowOffset");
                        var so = new SerializedObject(posComposer);
                        var offsetProp = so.FindProperty("CameraDistance");
                        if (offsetProp != null)
                        {
                            // Use magnitude as distance for position composer
                            offsetProp.floatValue = new Vector3(
                                p.FollowOffset[0], p.FollowOffset[1], p.FollowOffset[2]).magnitude;
                            so.ApplyModifiedProperties();
                        }
                        propsSet.Add("FollowOffset");
                    }
                }
            }

            if (p.Damping != null && p.Damping.Length == 3)
            {
                var posComposer = go.GetComponent<CinemachinePositionComposer>();
                if (posComposer != null)
                {
                    Undo.RecordObject(posComposer, "Mosaic: Cinemachine Set Damping");
                    var so = new SerializedObject(posComposer);
                    var dampingProp = so.FindProperty("Damping");
                    if (dampingProp != null)
                    {
                        dampingProp.vector3Value = new Vector3(
                            p.Damping[0], p.Damping[1], p.Damping[2]);
                        so.ApplyModifiedProperties();
                    }
                    propsSet.Add("Damping");
                }
            }

            EditorUtility.SetDirty(vcam);

            return ToolResult<CinemachineSetPropertiesResult>.Ok(new CinemachineSetPropertiesResult
            {
                VCamName = go.name,
                PropertiesSet = propsSet.ToArray()
            });
        }
    }
}
#endif
