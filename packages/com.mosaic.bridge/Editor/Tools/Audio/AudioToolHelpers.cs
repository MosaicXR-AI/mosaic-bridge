using UnityEngine;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Audio
{
    internal static class AudioToolHelpers
    {
        internal static string GetHierarchyPath(Transform t)
        {
            var path = t.name;
            var current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        /// <summary>Same InstanceId-then-Name resolution every audio tool already repeats
        /// (AudioCreateSourceTool, AudioSetSpatialTool) — factored here for the two new ones
        /// rather than adding a fourth copy.</summary>
        internal static GameObject ResolveGameObject(int? instanceId, string name)
        {
            GameObject go = null;
            if (instanceId.HasValue)
            {
#pragma warning disable CS0618
                go = UnityIds.Resolve(instanceId.Value) as GameObject;
#pragma warning restore CS0618
            }
            if (go == null && !string.IsNullOrEmpty(name))
                go = GameObject.Find(name);
            return go;
        }

        internal static AudioSource ResolveAudioSource(int? instanceId, string name, out GameObject go)
        {
            go = ResolveGameObject(instanceId, name);
            return go != null ? go.GetComponent<AudioSource>() : null;
        }
    }
}
