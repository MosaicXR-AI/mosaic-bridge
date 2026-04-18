using UnityEngine;
using UnityEditor;

namespace Mosaic.Bridge.Tools.Particles
{
    /// <summary>
    /// Shared helpers for particle tools: resolve a ParticleSystem by InstanceId or Name.
    /// </summary>
    internal static class ParticleToolHelpers
    {
        /// <summary>
        /// Resolve a ParticleSystem from optional InstanceId / Name.
        /// Returns null if neither is provided or the target cannot be found.
        /// </summary>
        public static ParticleSystem Resolve(int? instanceId, string name)
        {
            ParticleSystem ps = null;

            if (instanceId.HasValue)
            {
#pragma warning disable CS0618
                var obj = EditorUtility.InstanceIDToObject(instanceId.Value);
#pragma warning restore CS0618

                if (obj is ParticleSystem direct)
                    ps = direct;
                else if (obj is GameObject go)
                    ps = go.GetComponent<ParticleSystem>();
            }

            if (ps == null && !string.IsNullOrEmpty(name))
            {
                var go = GameObject.Find(name);
                if (go != null)
                    ps = go.GetComponent<ParticleSystem>();
            }

            return ps;
        }

        /// <summary>
        /// Build a hierarchy path string for a Transform.
        /// </summary>
        public static string GetHierarchyPath(Transform t)
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
    }
}
