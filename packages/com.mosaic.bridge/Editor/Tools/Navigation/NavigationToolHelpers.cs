using UnityEngine;
using UnityEditor;

namespace Mosaic.Bridge.Tools.Navigation
{
    internal static class NavigationToolHelpers
    {
        /// <summary>
        /// Resolves a GameObject by InstanceId (preferred) or Name.
        /// Returns null if neither resolves.
        /// </summary>
        internal static GameObject ResolveGameObject(int? instanceId, string name)
        {
            if (instanceId.HasValue)
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject;
                if (obj != null) return obj;
            }

            if (!string.IsNullOrEmpty(name))
                return GameObject.Find(name);

            return null;
        }

        internal static string GameObjectNotFoundMessage(int? instanceId, string name)
        {
            if (instanceId.HasValue)
                return $"GameObject with InstanceId {instanceId.Value} not found";
            if (!string.IsNullOrEmpty(name))
                return $"GameObject '{name}' not found";
            return "No InstanceId or Name provided to identify the GameObject";
        }
    }
}
