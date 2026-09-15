#if MOSAIC_HAS_CINEMACHINE
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineCreateBrainTool
    {
        [MosaicTool("cinemachine/create-brain",
                    "Adds a CinemachineBrain component to the main camera or a specified camera. " +
                    "UpdateMethod, WorldUpOverrideName, ChannelMask configure the brain. CustomBlends " +
                    "(From/To vcam names — '**ANY CAMERA**' matches any — + BlendType/BlendTime) are " +
                    "appended to a CinemachineBlenderSettings at CustomBlendsAssetPath, created if it " +
                    "doesn't exist yet, for the blend-rules lesson.",
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

            if (!string.IsNullOrEmpty(p.UpdateMethod))
            {
                if (!TryParseUpdateMethod(p.UpdateMethod, out var updateMethod))
                    return ToolResult<CinemachineCreateBrainResult>.Fail(
                        $"Unknown UpdateMethod '{p.UpdateMethod}'. Valid: FixedUpdate, LateUpdate, ManualUpdate, SmartUpdate",
                        ErrorCodes.INVALID_PARAM);
                brain.UpdateMethod = updateMethod;
            }

            if (!string.IsNullOrEmpty(p.WorldUpOverrideName))
            {
                var worldUpGo = GameObject.Find(p.WorldUpOverrideName);
                if (worldUpGo == null)
                    return ToolResult<CinemachineCreateBrainResult>.Fail(
                        $"GameObject '{p.WorldUpOverrideName}' not found", ErrorCodes.NOT_FOUND);
                brain.WorldUpOverride = worldUpGo.transform;
            }

            if (p.ChannelMask.HasValue)
                brain.ChannelMask = (OutputChannels)p.ChannelMask.Value;

            if (p.CustomBlends != null)
            {
                if (string.IsNullOrEmpty(p.CustomBlendsAssetPath))
                    return ToolResult<CinemachineCreateBrainResult>.Fail(
                        "CustomBlendsAssetPath is required when CustomBlends is given", ErrorCodes.INVALID_PARAM);

                var blenderSettings = AssetDatabase.LoadAssetAtPath<CinemachineBlenderSettings>(p.CustomBlendsAssetPath);
                var isNewAsset = blenderSettings == null;
                if (isNewAsset)
                    blenderSettings = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();

                var existing = blenderSettings.CustomBlends ?? new CinemachineBlenderSettings.CustomBlend[0];
                var appended = new CinemachineBlenderSettings.CustomBlend[existing.Length + p.CustomBlends.Length];
                existing.CopyTo(appended, 0);
                for (int i = 0; i < p.CustomBlends.Length; i++)
                {
                    var input = p.CustomBlends[i];
                    if (!TryParseBlendStyle(input.BlendType, out var style))
                        return ToolResult<CinemachineCreateBrainResult>.Fail(
                            $"Invalid BlendType '{input.BlendType}' in CustomBlends[{i}]. Valid: Cut, EaseInOut, Linear",
                            ErrorCodes.INVALID_PARAM);
                    appended[existing.Length + i] = new CinemachineBlenderSettings.CustomBlend
                    {
                        From = input.From,
                        To = input.To,
                        Blend = new CinemachineBlendDefinition(style, input.BlendTime),
                    };
                }
                blenderSettings.CustomBlends = appended;

                if (isNewAsset)
                    AssetDatabase.CreateAsset(blenderSettings, p.CustomBlendsAssetPath);
                else
                    EditorUtility.SetDirty(blenderSettings);
                AssetDatabase.SaveAssets();

                brain.CustomBlends = blenderSettings;
            }

            EditorUtility.SetDirty(brain);

            return ToolResult<CinemachineCreateBrainResult>.Ok(new CinemachineCreateBrainResult
            {
                InstanceId = UnityIds.Of(cam.gameObject),
                CameraName = cam.gameObject.name,
                DefaultBlend = p.DefaultBlend,
                BlendType = blendStyle.ToString(),
                AlreadyExisted = alreadyExisted,
                UpdateMethod = brain.UpdateMethod.ToString(),
                WorldUpOverrideName = brain.WorldUpOverride != null ? brain.WorldUpOverride.name : null,
                ChannelMask = (int)brain.ChannelMask,
                CustomBlendsAssetPath = brain.CustomBlends != null ? p.CustomBlendsAssetPath : null,
                CustomBlendCount = brain.CustomBlends != null ? brain.CustomBlends.CustomBlends.Length : 0,
            });
        }

        private static bool TryParseUpdateMethod(string value, out CinemachineBrain.UpdateMethods result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "fixedupdate":  result = CinemachineBrain.UpdateMethods.FixedUpdate;  return true;
                case "lateupdate":   result = CinemachineBrain.UpdateMethods.LateUpdate;   return true;
                case "manualupdate": result = CinemachineBrain.UpdateMethods.ManualUpdate; return true;
                case "smartupdate":  result = CinemachineBrain.UpdateMethods.SmartUpdate;  return true;
                default:             result = CinemachineBrain.UpdateMethods.SmartUpdate;  return false;
            }
        }

        private static bool TryParseBlendStyle(string value, out CinemachineBlendDefinition.Styles result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "cut":        result = CinemachineBlendDefinition.Styles.Cut;       return true;
                case "easeinout":  result = CinemachineBlendDefinition.Styles.EaseInOut; return true;
                case "linear":     result = CinemachineBlendDefinition.Styles.Linear;    return true;
                default:           result = CinemachineBlendDefinition.Styles.EaseInOut; return false;
            }
        }
    }
}
#endif
