#if MOSAIC_HAS_CINEMACHINE
using UnityEngine;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    internal static class CinemachineToolHelpers
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
