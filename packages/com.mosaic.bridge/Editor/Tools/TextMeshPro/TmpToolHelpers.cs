#if MOSAIC_HAS_TMP
using UnityEngine;

namespace Mosaic.Bridge.Tools.TextMeshPro
{
    internal static class TmpToolHelpers
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
