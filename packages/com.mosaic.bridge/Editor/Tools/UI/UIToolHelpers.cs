using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.UI
{
    internal static class UIToolHelpers
    {
        /// <summary>GameObject.GetComponent(string) does not reliably resolve a type by its bare
        /// name across namespaces (confirmed by a real failing test: even an unambiguous custom
        /// MonoBehaviour name failed to resolve) — this searches every loaded assembly instead,
        /// the same pattern used throughout this codebase's other ResolveType helpers.</summary>
        internal static Type ResolveComponentType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            var direct = Type.GetType(typeName);
            if (direct != null && typeof(Component).IsAssignableFrom(direct)) return direct;

            // Bare simple names (e.g. "Button") collide across loaded assemblies — Unity's own
            // InputSystem, for one, defines an unrelated non-Component "Button" enum. Only a real
            // Component/MonoBehaviour is ever a valid answer here, and an exact FullName match
            // wins over a same-named-elsewhere type when both exist.
            var candidates = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .Where(t => typeof(Component).IsAssignableFrom(t) && (t.Name == typeName || t.FullName == typeName))
                .ToList();
            if (candidates.Count == 0) return null;
            return candidates.FirstOrDefault(t => t.FullName == typeName) ?? candidates[0];
        }

        /// <summary>
        /// Resolves a GameObject by InstanceId first, then by Name.
        /// Returns null if neither resolves.
        /// </summary>
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
    }
}
