#if MOSAIC_HAS_SPLINES
using UnityEngine;

namespace Mosaic.Bridge.Tools.Splines
{
    internal static class SplineToolHelpers
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
