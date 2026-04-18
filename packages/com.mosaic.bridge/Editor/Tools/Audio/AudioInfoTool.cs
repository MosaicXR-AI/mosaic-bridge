using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioInfoTool
    {
        [MosaicTool("audio/info",
                    "Queries audio state: lists all AudioSources and AudioListeners with properties and warnings",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<AudioInfoResult> Execute(AudioInfoParams p)
        {
            var sources = new List<AudioSourceInfo>();
            var listeners = new List<AudioListenerInfo>();
            var warnings = new List<string>();

            bool sceneWide = p.InstanceId == null && string.IsNullOrEmpty(p.Name);

            if (sceneWide)
            {
                // Scan entire scene
                foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    sources.Add(MapSource(source));

                foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    listeners.Add(MapListener(listener));
            }
            else
            {
                // Query specific GameObject
                GameObject go = null;

                if (p.InstanceId.HasValue)
                {
#pragma warning disable CS0618
                    go = EditorUtility.InstanceIDToObject(p.InstanceId.Value) as GameObject;
#pragma warning restore CS0618
                }

                if (go == null && !string.IsNullOrEmpty(p.Name))
                    go = GameObject.Find(p.Name);

                if (go == null)
                    return ToolResult<AudioInfoResult>.Fail(
                        $"GameObject not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                        ErrorCodes.NOT_FOUND);

                foreach (var source in go.GetComponents<AudioSource>())
                    sources.Add(MapSource(source));

                var listener = go.GetComponent<AudioListener>();
                if (listener != null)
                    listeners.Add(MapListener(listener));
            }

            // Generate scene-wide listener warnings (always check full scene)
            var sceneListenerCount = sceneWide
                ? listeners.Count
                : Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;

            if (sceneListenerCount == 0)
                warnings.Add("No AudioListener found in scene. Audio will not be heard during playback.");

            if (sceneListenerCount > 1)
                warnings.Add($"Multiple AudioListeners found ({sceneListenerCount}). Unity only uses one at a time; this may cause unexpected behavior.");

            foreach (var src in sources)
            {
                if (string.IsNullOrEmpty(src.ClipName))
                    warnings.Add($"AudioSource on '{src.GameObjectName}' has no AudioClip assigned.");
            }

            return ToolResult<AudioInfoResult>.Ok(new AudioInfoResult
            {
                Sources   = sources,
                Listeners = listeners,
                Warnings  = warnings
            });
        }

        private static AudioSourceInfo MapSource(AudioSource source)
        {
            return new AudioSourceInfo
            {
                InstanceId    = source.gameObject.GetInstanceID(),
                GameObjectName = source.gameObject.name,
                HierarchyPath = AudioToolHelpers.GetHierarchyPath(source.transform),
                ClipName      = source.clip != null ? source.clip.name : null,
                Volume        = source.volume,
                Pitch         = source.pitch,
                SpatialBlend  = source.spatialBlend,
                Loop          = source.loop,
                PlayOnAwake   = source.playOnAwake,
                IsPlaying     = source.isPlaying,
                MinDistance    = source.minDistance,
                MaxDistance    = source.maxDistance,
                RolloffMode   = source.rolloffMode.ToString(),
                DopplerLevel  = source.dopplerLevel,
                Spread        = source.spread
            };
        }

        private static AudioListenerInfo MapListener(AudioListener listener)
        {
            return new AudioListenerInfo
            {
                InstanceId     = listener.gameObject.GetInstanceID(),
                GameObjectName = listener.gameObject.name,
                HierarchyPath  = AudioToolHelpers.GetHierarchyPath(listener.transform),
                Enabled        = listener.enabled
            };
        }
    }
}
