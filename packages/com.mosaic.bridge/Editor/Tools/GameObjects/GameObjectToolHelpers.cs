using UnityEngine;

namespace Mosaic.Bridge.Tools.GameObjects
{
    internal static class GameObjectToolHelpers
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

        internal static float[] ToFloatArray(Vector3 v) => new[] { v.x, v.y, v.z };
    }
}
