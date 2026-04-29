using System;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioSetSpatialTool
    {
        [MosaicTool("audio/set-spatial",
                    "Sets spatial audio properties on an AudioSource (min/max distance, rolloff, doppler, spread)",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AudioSetSpatialResult> Execute(AudioSetSpatialParams p)
        {
            // 1. Require at least one identifier
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<AudioSetSpatialResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            // 2. Resolve target GameObject
            GameObject go = null;

            if (p.InstanceId.HasValue)
            {
#pragma warning disable CS0618
                go = UnityEngine.Resources.EntityIdToObject(p.InstanceId.Value) as GameObject;
#pragma warning restore CS0618
            }

            if (go == null && !string.IsNullOrEmpty(p.Name))
                go = GameObject.Find(p.Name);

            if (go == null)
                return ToolResult<AudioSetSpatialResult>.Fail(
                    $"GameObject not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                    ErrorCodes.NOT_FOUND);

            // 3. Find AudioSource component
            var source = go.GetComponent<AudioSource>();
            if (source == null)
                return ToolResult<AudioSetSpatialResult>.Fail(
                    $"No AudioSource found on GameObject '{go.name}'", ErrorCodes.NOT_FOUND);

            // 4. Record for undo
            Undo.RecordObject(source, "Mosaic: Set Spatial Audio");

            // 5. Apply spatial properties
            if (p.MinDistance.HasValue)
                source.minDistance = Mathf.Max(0f, p.MinDistance.Value);

            if (p.MaxDistance.HasValue)
                source.maxDistance = Mathf.Max(source.minDistance, p.MaxDistance.Value);

            if (!string.IsNullOrEmpty(p.RolloffMode))
            {
                if (Enum.TryParse<AudioRolloffMode>(p.RolloffMode, ignoreCase: true, out var mode))
                    source.rolloffMode = mode;
                else
                    return ToolResult<AudioSetSpatialResult>.Fail(
                        $"Invalid RolloffMode '{p.RolloffMode}'. Use Logarithmic, Linear, or Custom.",
                        ErrorCodes.INVALID_PARAM);
            }

            if (p.DopplerLevel.HasValue)
                source.dopplerLevel = Mathf.Clamp(p.DopplerLevel.Value, 0f, 5f);

            if (p.Spread.HasValue)
                source.spread = Mathf.Clamp(p.Spread.Value, 0f, 360f);

#if UNITY_2023_1_OR_NEWER
            bool hasListener = UnityEngine.Object.FindAnyObjectByType<AudioListener>() != null;
#else
            bool hasListener = UnityEngine.Object.FindObjectOfType<AudioListener>() != null;
#endif
            string listenerWarning = hasListener
                ? null
                : "No AudioListener found in the scene — spatial audio will be silent. Add an AudioListener component (usually on the Main Camera).";

            return ToolResult<AudioSetSpatialResult>.Ok(new AudioSetSpatialResult
            {
                InstanceId             = go.GetInstanceID(),
                GameObjectName         = go.name,
                MinDistance            = source.minDistance,
                MaxDistance            = source.maxDistance,
                RolloffMode            = source.rolloffMode.ToString(),
                DopplerLevel           = source.dopplerLevel,
                Spread                 = source.spread,
                NoAudioListenerWarning = listenerWarning
            });
        }
    }
}
