using UnityEngine;

namespace Mosaic.Bridge.Tools.Lighting
{
    internal static class LightingToolHelpers
    {
        /// <summary>
        /// Find a Light component by instance ID or by GameObject name.
        /// Returns null if nothing matched.
        /// </summary>
        internal static Light FindLight(int instanceId, string name)
        {
            if (instanceId != 0)
            {
                var obj = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);
                if (obj is GameObject go)
                    return go.GetComponent<Light>();
                if (obj is Light light)
                    return light;
                return null;
            }

            if (!string.IsNullOrEmpty(name))
            {
                var go = GameObject.Find(name);
                return go != null ? go.GetComponent<Light>() : null;
            }

            return null;
        }

        /// <summary>
        /// Build the full hierarchy path for a transform.
        /// </summary>
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

        /// <summary>
        /// Parse a float[3] or float[4] array into a Color.
        /// Returns null if the array is null or has fewer than 3 elements.
        /// </summary>
        internal static Color? ParseColor(float[] c)
        {
            if (c == null || c.Length < 3) return null;
            float a = c.Length >= 4 ? c[3] : 1f;
            return new Color(c[0], c[1], c[2], a);
        }
    }
}
