using UnityEngine;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Renderers
{
    internal static class RendererToolHelpers
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

        internal static Color ParseColor(float[] rgba, Color fallback)
        {
            if (rgba == null || rgba.Length < 3) return fallback;
            return new Color(rgba[0], rgba[1], rgba[2], rgba.Length >= 4 ? rgba[3] : 1f);
        }
    }
}
