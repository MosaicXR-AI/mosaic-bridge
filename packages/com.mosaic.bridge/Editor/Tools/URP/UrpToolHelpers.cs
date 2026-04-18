#if MOSAIC_HAS_URP
using UnityEngine;

namespace Mosaic.Bridge.Tools.URP
{
    internal static class UrpToolHelpers
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
    }
}
#endif
