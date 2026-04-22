using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioCreateSourceTool
    {
        [MosaicTool("audio/create-source",
                    "Creates an AudioSource component on a GameObject. Creates a new GameObject if no target is specified.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AudioCreateSourceResult> Execute(AudioCreateSourceParams p)
        {
            // 1. Resolve or create target GameObject
            GameObject go = null;

            if (p.InstanceId.HasValue)
            {
#pragma warning disable CS0618
                go = UnityEngine.Resources.EntityIdToObject(p.InstanceId.Value) as GameObject;
#pragma warning restore CS0618
            }

            if (go == null && !string.IsNullOrEmpty(p.Name))
                go = GameObject.Find(p.Name);

            bool createdNewGo = false;
            if (go == null)
            {
                string goName = !string.IsNullOrEmpty(p.Name) ? p.Name : "AudioSource";
                go = new GameObject(goName);
                Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create AudioSource GameObject");
                createdNewGo = true;
            }

            // 2. Add AudioSource component
            var source = createdNewGo
                ? go.AddComponent<AudioSource>()
                : Undo.AddComponent<AudioSource>(go);

            // 3. Load clip if path provided
            AudioClip clip = null;
            if (!string.IsNullOrEmpty(p.ClipPath))
            {
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p.ClipPath);
                if (clip == null)
                    return ToolResult<AudioCreateSourceResult>.Fail(
                        $"AudioClip not found at path: '{p.ClipPath}'", ErrorCodes.NOT_FOUND);
            }

            // 4. Configure properties
            if (!createdNewGo)
                Undo.RecordObject(source, "Mosaic: Configure AudioSource");

            if (clip != null)
                source.clip = clip;

            source.volume      = Mathf.Clamp01(p.Volume ?? 1f);
            source.pitch       = p.Pitch ?? 1f;
            source.spatialBlend = Mathf.Clamp01(p.SpatialBlend ?? 0f);
            source.loop        = p.Loop ?? false;
            source.playOnAwake = p.PlayOnAwake ?? true;

            return ToolResult<AudioCreateSourceResult>.Ok(new AudioCreateSourceResult
            {
                InstanceId          = go.GetInstanceID(),
                GameObjectName      = go.name,
                HierarchyPath       = AudioToolHelpers.GetHierarchyPath(go.transform),
                ComponentInstanceId = source.GetInstanceID(),
                Volume              = source.volume,
                Pitch               = source.pitch,
                SpatialBlend        = source.spatialBlend,
                Loop                = source.loop,
                PlayOnAwake         = source.playOnAwake,
                ClipName            = clip != null ? clip.name : null
            });
        }
    }
}
