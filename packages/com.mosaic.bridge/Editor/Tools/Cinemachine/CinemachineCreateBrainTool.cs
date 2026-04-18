#if MOSAIC_HAS_CINEMACHINE
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineCreateBrainTool
    {
        [MosaicTool("cinemachine/create-brain",
                    "Adds a CinemachineBrain component to the main camera or a specified camera",
                    isReadOnly: false)]
        public static ToolResult<CinemachineCreateBrainResult> Execute(CinemachineCreateBrainParams p)
        {
            // Find the target camera
            Camera cam;
            if (!string.IsNullOrEmpty(p.CameraName))
            {
                var go = GameObject.Find(p.CameraName);
                if (go == null)
                    return ToolResult<CinemachineCreateBrainResult>.Fail(
                        $"Camera GameObject '{p.CameraName}' not found", ErrorCodes.NOT_FOUND);
                cam = go.GetComponent<Camera>();
                if (cam == null)
                    return ToolResult<CinemachineCreateBrainResult>.Fail(
                        $"GameObject '{p.CameraName}' does not have a Camera component", ErrorCodes.INVALID_PARAM);
            }
            else
            {
                cam = Camera.main;
                if (cam == null)
                    return ToolResult<CinemachineCreateBrainResult>.Fail(
                        "No main camera found in scene. Tag a camera as 'MainCamera' or specify CameraName.",
                        ErrorCodes.NOT_FOUND);
            }

            // Parse blend style
            CinemachineBlendDefinition.Styles blendStyle;
            switch ((p.BlendType ?? "EaseInOut").ToLowerInvariant())
            {
                case "cut":
                    blendStyle = CinemachineBlendDefinition.Styles.Cut;
                    break;
                case "easeinout":
                    blendStyle = CinemachineBlendDefinition.Styles.EaseInOut;
                    break;
                case "linear":
                    blendStyle = CinemachineBlendDefinition.Styles.Linear;
                    break;
                default:
                    return ToolResult<CinemachineCreateBrainResult>.Fail(
                        $"Invalid BlendType '{p.BlendType}'. Valid: Cut, EaseInOut, Linear",
                        ErrorCodes.INVALID_PARAM);
            }

            // Check if brain already exists
            bool alreadyExisted = false;
            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain != null)
            {
                alreadyExisted = true;
            }
            else
            {
                brain = Undo.AddComponent<CinemachineBrain>(cam.gameObject);
            }

            brain.DefaultBlend = new CinemachineBlendDefinition(blendStyle, p.DefaultBlend);

            return ToolResult<CinemachineCreateBrainResult>.Ok(new CinemachineCreateBrainResult
            {
                InstanceId = cam.gameObject.GetInstanceID(),
                CameraName = cam.gameObject.name,
                DefaultBlend = p.DefaultBlend,
                BlendType = blendStyle.ToString(),
                AlreadyExisted = alreadyExisted
            });
        }
    }
}
#endif
