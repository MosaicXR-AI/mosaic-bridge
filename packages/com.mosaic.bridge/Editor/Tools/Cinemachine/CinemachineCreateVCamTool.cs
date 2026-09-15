#if MOSAIC_HAS_CINEMACHINE
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;
#if MOSAIC_HAS_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineCreateVCamTool
    {
        [MosaicTool("cinemachine/create-vcam",
                    "Creates a new CinemachineCamera (virtual camera). Body: ThirdPersonFollow, " +
                    "OrbitalFollow, PositionComposer, Follow (the most common — FollowOffset), " +
                    "HardLockToTarget. Aim: Composer, HardLookAt, GroupFraming, PanTilt (Pan/TiltAngle), " +
                    "RotateWithFollowTarget. Noise: BasicMultiChannelPerlin (NoiseProfilePath or " +
                    "NoiseProfilePresetName + Amplitude/FrequencyGain). Lens: Dutch, OrthographicSize, " +
                    "LensModeOverride. AddInputController adds a CinemachineInputAxisController and " +
                    "auto-discovers the vcam's own input axes (needs Input System) — without it an " +
                    "orbit/look camera is inert in Play mode; InputControllerBindings rebinds a " +
                    "discovered controller (by name) to a custom InputActionReference asset.",
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
                    case "follow":
                        var follow = go.AddComponent<CinemachineFollow>();
                        if (p.FollowOffset != null)
                        {
                            if (p.FollowOffset.Length != 3)
                                return Fail(go, "FollowOffset requires exactly [x, y, z]");
                            follow.FollowOffset = new Vector3(p.FollowOffset[0], p.FollowOffset[1], p.FollowOffset[2]);
                        }
                        bodyAdded = "Follow";
                        break;
                    case "hardlocktotarget":
                        go.AddComponent<CinemachineHardLockToTarget>();
                        bodyAdded = "HardLockToTarget";
                        break;
                    default:
                        return Fail(go, $"Invalid BodyType '{p.BodyType}'. Valid: ThirdPersonFollow, OrbitalFollow, " +
                                         "PositionComposer, Follow, HardLockToTarget");
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
                    case "pantilt":
                        var panTilt = go.AddComponent<CinemachinePanTilt>();
                        if (p.PanAngle.HasValue) panTilt.PanAxis.Value = p.PanAngle.Value;
                        if (p.TiltAngle.HasValue) panTilt.TiltAxis.Value = p.TiltAngle.Value;
                        aimAdded = "PanTilt";
                        break;
                    case "rotatewithfollowtarget":
                        go.AddComponent<CinemachineRotateWithFollowTarget>();
                        aimAdded = "RotateWithFollowTarget";
                        break;
                    default:
                        return Fail(go, $"Invalid AimType '{p.AimType}'. Valid: Composer, HardLookAt, GroupFraming, " +
                                         "PanTilt, RotateWithFollowTarget");
                }
            }

            // Noise component
            string noiseAdded = null;
            if (!string.IsNullOrEmpty(p.NoiseType))
            {
                switch (p.NoiseType.ToLowerInvariant())
                {
                    case "basicmultichannelperlin":
                        var perlin = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
                        if (!string.IsNullOrEmpty(p.NoiseProfilePath) || !string.IsNullOrEmpty(p.NoiseProfilePresetName))
                        {
                            if (!CinemachineToolHelpers.TryResolveNoiseProfile(
                                    p.NoiseProfilePath, p.NoiseProfilePresetName, out var profile, out var profileError))
                                return Fail(go, profileError);
                            perlin.NoiseProfile = profile;
                        }
                        if (p.NoiseAmplitudeGain.HasValue) perlin.AmplitudeGain = p.NoiseAmplitudeGain.Value;
                        if (p.NoiseFrequencyGain.HasValue) perlin.FrequencyGain = p.NoiseFrequencyGain.Value;
                        noiseAdded = "BasicMultiChannelPerlin";
                        break;
                    default:
                        return Fail(go, $"Invalid NoiseType '{p.NoiseType}'. Valid: BasicMultiChannelPerlin");
                }
            }

            // Lens block
            if (p.Dutch.HasValue || p.OrthographicSize.HasValue || !string.IsNullOrEmpty(p.LensModeOverride))
            {
                var lens = vcam.Lens;
                if (p.Dutch.HasValue) lens.Dutch = p.Dutch.Value;
                if (p.OrthographicSize.HasValue) lens.OrthographicSize = p.OrthographicSize.Value;
                if (!string.IsNullOrEmpty(p.LensModeOverride))
                {
                    if (!CinemachineToolHelpers.TryParseLensModeOverride(p.LensModeOverride, out var modeOverride))
                        return Fail(go, $"Invalid LensModeOverride '{p.LensModeOverride}'. Valid: None, Orthographic, Perspective, Physical");
                    lens.ModeOverride = modeOverride;
                }
                vcam.Lens = lens;
            }

            // Input axis controller: without this, an orbit/look camera (PanTilt, OrbitalFollow)
            // has axes but nothing driving them, so it's inert in Play mode.
            bool inputControllerAdded = false;
            string[] discoveredControllerNames = null;
            if (p.AddInputController)
            {
#if MOSAIC_HAS_INPUT_SYSTEM
                var axisController = go.AddComponent<CinemachineInputAxisController>();
                axisController.SynchronizeControllers();
                inputControllerAdded = true;
                discoveredControllerNames = axisController.Controllers.Select(c => c.Name).ToArray();

                if (p.InputControllerBindings != null)
                {
                    foreach (var binding in p.InputControllerBindings)
                    {
                        var index = axisController.Controllers.FindIndex(c => c.Name == binding.ControllerName);
                        if (index < 0)
                            return Fail(go, $"No discovered controller named '{binding.ControllerName}'. " +
                                             $"Available: {string.Join(", ", discoveredControllerNames)}", ErrorCodes.NOT_FOUND);

                        var actionRef = AssetDatabase.LoadAssetAtPath<InputActionReference>(binding.InputActionReferencePath);
                        if (actionRef == null)
                            return Fail(go, $"No InputActionReference found at '{binding.InputActionReferencePath}'", ErrorCodes.NOT_FOUND);

                        var controllerEntry = axisController.Controllers[index];
                        var input = controllerEntry.Input;
                        input.InputAction = actionRef;
                        if (binding.Gain.HasValue) input.Gain = binding.Gain.Value;
                        controllerEntry.Input = input;
                        axisController.Controllers[index] = controllerEntry;
                    }
                }
#else
                return Fail(go, "AddInputController requires the com.unity.inputsystem package, which is not installed in this project.");
#endif
            }

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Cinemachine Create VCam");

            return ToolResult<CinemachineCreateVCamResult>.Ok(new CinemachineCreateVCamResult
            {
                InstanceId = UnityIds.Of(go),
                Name = go.name,
                HierarchyPath = CinemachineToolHelpers.GetHierarchyPath(go.transform),
                BodyType = bodyAdded,
                AimType = aimAdded,
                NoiseType = noiseAdded,
                Priority = p.Priority,
                Dutch = vcam.Lens.Dutch,
                OrthographicSize = vcam.Lens.OrthographicSize,
                LensModeOverride = vcam.Lens.ModeOverride.ToString(),
                InputControllerAdded = inputControllerAdded,
                DiscoveredControllerNames = discoveredControllerNames,
            });
        }

        private static ToolResult<CinemachineCreateVCamResult> Fail(GameObject toDestroy, string message, string code = ErrorCodes.INVALID_PARAM)
        {
            Object.DestroyImmediate(toDestroy);
            return ToolResult<CinemachineCreateVCamResult>.Fail(message, code);
        }
    }
}
#endif
