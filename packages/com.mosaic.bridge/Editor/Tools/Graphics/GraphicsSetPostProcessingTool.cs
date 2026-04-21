#if UNITY_6000_0_OR_NEWER
using System.Reflection;
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

            // Unity's Volume declares isGlobal, weight, priority, and sharedProfile as
            // public FIELDS, not properties. Using GetProperty alone silently no-ops.
            // Try property first (in case of custom subclass), then fall back to field.
            SetMember(volumeType, volume, "isGlobal", p.IsGlobal);
            SetMember(volumeType, volume, "weight", Mathf.Clamp01(p.Weight));
            SetMember(volumeType, volume, "priority", p.Priority);

            if (!string.IsNullOrEmpty(p.ProfilePath))
            {
                var profile = AssetDatabase.LoadAssetAtPath<Object>(p.ProfilePath);
                if (profile == null)
                    return ToolResult<GraphicsSetPostProcessingResult>.Fail(
                        $"VolumeProfile not found at '{p.ProfilePath}'", ErrorCodes.NOT_FOUND);

                SetMember(volumeType, volume, "sharedProfile", profile);
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

        // Sets a member by name, trying property first, then field. Returns whether
        // the member was found (call sites may want to know, but current callers
        // treat absence as silent-ok since not all Volume subclasses expose the same
        // members across pipeline versions).
        private static bool SetMember(System.Type type, object target, string memberName, object value)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var prop = type.GetProperty(memberName, flags);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(target, value);
                return true;
            }
            var field = type.GetField(memberName, flags);
            if (field != null)
            {
                field.SetValue(target, value);
                return true;
            }
            return false;
        }
    }
}
#endif
