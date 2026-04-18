#if MOSAIC_HAS_CINEMACHINE
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineInfoTool
    {
        [MosaicTool("cinemachine/info",
                    "Queries all Cinemachine virtual cameras and brains in the scene, returning their configuration and live status",
                    isReadOnly: true)]
        public static ToolResult<CinemachineInfoResult> Execute(CinemachineInfoParams p)
        {
            // Gather all virtual cameras
            var allVCams = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
            var vcamInfos = new List<CinemachineVCamInfo>();

            // Gather all brains to check live status
            var allBrains = Object.FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None);

            // Build a set of live cameras
            var liveCamNames = new HashSet<string>();
            foreach (var brain in allBrains)
            {
                var active = brain.ActiveVirtualCamera as CinemachineCamera;
                if (active != null)
                    liveCamNames.Add(active.gameObject.name);
            }

            foreach (var vcam in allVCams)
            {
                if (!string.IsNullOrEmpty(p.VCamName) && vcam.gameObject.name != p.VCamName)
                    continue;

                var bodyComponents = new List<string>();
                var aimComponents = new List<string>();

                if (vcam.GetComponent<CinemachineThirdPersonFollow>() != null)
                    bodyComponents.Add("ThirdPersonFollow");
                if (vcam.GetComponent<CinemachineOrbitalFollow>() != null)
                    bodyComponents.Add("OrbitalFollow");
                if (vcam.GetComponent<CinemachinePositionComposer>() != null)
                    bodyComponents.Add("PositionComposer");

                if (vcam.GetComponent<CinemachineRotationComposer>() != null)
                    aimComponents.Add("Composer");
                if (vcam.GetComponent<CinemachineHardLookAt>() != null)
                    aimComponents.Add("HardLookAt");
                if (vcam.GetComponent<CinemachineGroupFraming>() != null)
                    aimComponents.Add("GroupFraming");

                vcamInfos.Add(new CinemachineVCamInfo
                {
                    InstanceId = vcam.gameObject.GetInstanceID(),
                    Name = vcam.gameObject.name,
                    Priority = (int)vcam.Priority.Value,
                    FollowTarget = vcam.Follow != null ? vcam.Follow.name : null,
                    LookAtTarget = vcam.LookAt != null ? vcam.LookAt.name : null,
                    IsLive = liveCamNames.Contains(vcam.gameObject.name),
                    HierarchyPath = CinemachineToolHelpers.GetHierarchyPath(vcam.transform),
                    BodyComponents = bodyComponents.ToArray(),
                    AimComponents = aimComponents.ToArray()
                });
            }

            // Gather brain info
            var brainInfos = new List<CinemachineBrainInfo>();
            string activeCamera = null;
            foreach (var brain in allBrains)
            {
                brainInfos.Add(new CinemachineBrainInfo
                {
                    InstanceId = brain.gameObject.GetInstanceID(),
                    CameraName = brain.gameObject.name,
                    DefaultBlendTime = brain.DefaultBlend.Time,
                    DefaultBlendStyle = brain.DefaultBlend.Style.ToString()
                });

                var activeCam = brain.ActiveVirtualCamera as CinemachineCamera;
                if (activeCam != null && activeCamera == null)
                    activeCamera = activeCam.gameObject.name;
            }

            return ToolResult<CinemachineInfoResult>.Ok(new CinemachineInfoResult
            {
                VirtualCameras = vcamInfos.ToArray(),
                Brains = brainInfos.ToArray(),
                ActiveCamera = activeCamera
            });
        }
    }
}
#endif
