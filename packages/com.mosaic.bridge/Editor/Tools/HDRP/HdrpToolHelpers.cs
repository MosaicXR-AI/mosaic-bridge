#if MOSAIC_HAS_HDRP
using UnityEngine;

namespace Mosaic.Bridge.Tools.HDRP
{
    internal static class HdrpToolHelpers
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
