using UnityEngine;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Physics2D
{
    internal static class Physics2DToolHelpers
    {
        internal static GameObject ResolveGameObject(int? instanceId, string name)
        {
            GameObject go = null;
            if (instanceId.HasValue && instanceId.Value != 0)
            {
#pragma warning disable CS0618
                go = UnityIds.Resolve(instanceId.Value) as GameObject;
#pragma warning restore CS0618
            }
            if (go == null && !string.IsNullOrEmpty(name))
                go = GameObject.Find(name);
            return go;
        }
    }
}
