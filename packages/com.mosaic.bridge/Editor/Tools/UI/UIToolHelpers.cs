using UnityEngine;
using UnityEditor;

namespace Mosaic.Bridge.Tools.UI
{
    internal static class UIToolHelpers
    {
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
                go = UnityEngine.Resources.EntityIdToObject(instanceId.Value) as GameObject;
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
