using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
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

        /// <summary>Resolves a group via the public AudioMixer.FindMatchingGroups, requiring
        /// exactly one match — the same disambiguation audio/route-source already needed, now
        /// shared with audio/mixer-group. An empty/null groupPath matches every group, so callers
        /// wanting "the master group" should resolve that separately rather than pass empty here.</summary>
        internal static bool TryResolveMixerGroup(AudioMixer mixer, string groupPath, out AudioMixerGroup group, out string error)
        {
            group = null;
            var matches = mixer.FindMatchingGroups(groupPath ?? "");
            if (matches == null || matches.Length == 0)
            {
                error = $"No group matching '{groupPath}' found in mixer '{mixer.name}'.";
                return false;
            }
            if (matches.Length > 1)
            {
                error = $"'{groupPath}' matches {matches.Length} groups in '{mixer.name}': " +
                        $"{string.Join(", ", matches.Select(g => g.name))}. Use a more specific sub-path " +
                        "(e.g. 'Master/SFX') to disambiguate.";
                return false;
            }
            group = matches[0];
            error = null;
            return true;
        }
    }
}
