#if UNITY_6000_0_OR_NEWER
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Graphics
{
    public static class GraphicsSetPostProcessingTool
    {
        [MosaicTool("graphics/set-post-processing",
                    "Configures a Volume component for post-processing on a GameObject",
                    isReadOnly: false)]
        public static ToolResult<GraphicsSetPostProcessingResult> SetPostProcessing(GraphicsSetPostProcessingParams p)
        {
            GameObject go = null;
            if (p.InstanceId != 0)
                go = EditorUtility.InstanceIDToObject(p.InstanceId) as GameObject;
            if (go == null && !string.IsNullOrEmpty(p.Name))
                go = GameObject.Find(p.Name);
            if (go == null)
                return ToolResult<GraphicsSetPostProcessingResult>.Fail(
                    "GameObject not found. Provide a valid InstanceId or Name.",
                    ErrorCodes.NOT_FOUND);

            // Volume requires a render pipeline (URP or HDRP)
            var volumeType = System.Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
            if (volumeType == null)
                return ToolResult<GraphicsSetPostProcessingResult>.Fail(
                    "Volume component requires URP or HDRP render pipeline to be installed.",
                    ErrorCodes.NOT_FOUND);

            var volume = go.GetComponent(volumeType);
            bool isNew = volume == null;
            if (isNew)
            {
                volume = Undo.AddComponent(go, volumeType);
            }
            else
            {
                Undo.RecordObject(volume, "Mosaic: Set Post-Processing");
            }

            // Use reflection to set properties (avoids hard dependency on URP assembly)
            var isGlobalProp = volumeType.GetProperty("isGlobal");
            var weightProp = volumeType.GetProperty("weight");
            var priorityProp = volumeType.GetProperty("priority");

            if (isGlobalProp != null) isGlobalProp.SetValue(volume, p.IsGlobal);
            if (weightProp != null) weightProp.SetValue(volume, Mathf.Clamp01(p.Weight));
            if (priorityProp != null) priorityProp.SetValue(volume, p.Priority);

            if (!string.IsNullOrEmpty(p.ProfilePath))
            {
                var profile = AssetDatabase.LoadAssetAtPath<Object>(p.ProfilePath);
                if (profile == null)
                    return ToolResult<GraphicsSetPostProcessingResult>.Fail(
                        $"VolumeProfile not found at '{p.ProfilePath}'", ErrorCodes.NOT_FOUND);

                var sharedProfileProp = volumeType.GetProperty("sharedProfile");
                if (sharedProfileProp != null) sharedProfileProp.SetValue(volume, profile);
            }

            EditorUtility.SetDirty(volume);

            return ToolResult<GraphicsSetPostProcessingResult>.Ok(new GraphicsSetPostProcessingResult
            {
                InstanceId = go.GetInstanceID(),
                GameObjectName = go.name,
                IsGlobal = p.IsGlobal,
                Weight = Mathf.Clamp01(p.Weight),
                Priority = p.Priority,
                ProfilePath = p.ProfilePath
            });
        }
    }
}
#endif
