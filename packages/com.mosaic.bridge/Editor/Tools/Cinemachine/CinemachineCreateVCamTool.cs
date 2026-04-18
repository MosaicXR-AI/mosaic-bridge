#if MOSAIC_HAS_CINEMACHINE
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineCreateVCamTool
    {
        [MosaicTool("cinemachine/create-vcam",
                    "Creates a new CinemachineCamera (virtual camera) with optional body/aim components and follow/look-at targets",
                    isReadOnly: false)]
        public static ToolResult<CinemachineCreateVCamResult> Execute(CinemachineCreateVCamParams p)
        {
            var go = new GameObject(p.Name);
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Priority.Value = p.Priority;

            // Follow target
            if (!string.IsNullOrEmpty(p.FollowTarget))
            {
                var followGo = GameObject.Find(p.FollowTarget);
                if (followGo == null)
                    return Fail(go, $"Follow target '{p.FollowTarget}' not found");
                vcam.Follow = followGo.transform;
            }

            // LookAt target
            if (!string.IsNullOrEmpty(p.LookAtTarget))
            {
                var lookAtGo = GameObject.Find(p.LookAtTarget);
                if (lookAtGo == null)
                    return Fail(go, $"LookAt target '{p.LookAtTarget}' not found");
                vcam.LookAt = lookAtGo.transform;
            }

            // Body component
            string bodyAdded = null;
            if (!string.IsNullOrEmpty(p.BodyType))
            {
                switch (p.BodyType.ToLowerInvariant())
                {
                    case "thirdpersonfollow":
                        go.AddComponent<CinemachineThirdPersonFollow>();
                        bodyAdded = "ThirdPersonFollow";
                        break;
                    case "orbitalfollow":
                        go.AddComponent<CinemachineOrbitalFollow>();
                        bodyAdded = "OrbitalFollow";
                        break;
                    case "positioncomposer":
                        go.AddComponent<CinemachinePositionComposer>();
                        bodyAdded = "PositionComposer";
                        break;
                    default:
                        return Fail(go, $"Invalid BodyType '{p.BodyType}'. Valid: ThirdPersonFollow, OrbitalFollow, PositionComposer");
                }
            }

            // Aim component
            string aimAdded = null;
            if (!string.IsNullOrEmpty(p.AimType))
            {
                switch (p.AimType.ToLowerInvariant())
                {
                    case "composer":
                        go.AddComponent<CinemachineRotationComposer>();
                        aimAdded = "Composer";
                        break;
                    case "hardlookat":
                        go.AddComponent<CinemachineHardLookAt>();
                        aimAdded = "HardLookAt";
                        break;
                    case "groupframing":
                        go.AddComponent<CinemachineGroupFraming>();
                        aimAdded = "GroupFraming";
                        break;
                    default:
                        return Fail(go, $"Invalid AimType '{p.AimType}'. Valid: Composer, HardLookAt, GroupFraming");
                }
            }

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Cinemachine Create VCam");

            return ToolResult<CinemachineCreateVCamResult>.Ok(new CinemachineCreateVCamResult
            {
                InstanceId = go.GetInstanceID(),
                Name = go.name,
                HierarchyPath = CinemachineToolHelpers.GetHierarchyPath(go.transform),
                BodyType = bodyAdded,
                AimType = aimAdded,
                Priority = p.Priority
            });
        }

        private static ToolResult<CinemachineCreateVCamResult> Fail(GameObject toDestroy, string message)
        {
            Object.DestroyImmediate(toDestroy);
            return ToolResult<CinemachineCreateVCamResult>.Fail(message, ErrorCodes.INVALID_PARAM);
        }
    }
}
#endif
