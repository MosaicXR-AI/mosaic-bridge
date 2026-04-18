using UnityEngine;
using UnityEditor;

namespace Mosaic.Bridge.Tools.Physics
{
    internal static class PhysicsToolHelpers
    {
        /// <summary>
        /// Resolves a GameObject by InstanceId (priority) or Name.
        /// Returns null if neither resolves.
        /// </summary>
        internal static GameObject ResolveGameObject(int? instanceId, string name)
        {
            GameObject go = null;

            if (instanceId.HasValue && instanceId.Value != 0)
            {
#pragma warning disable CS0618
                go = EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject;
#pragma warning restore CS0618
            }

            if (go == null && !string.IsNullOrEmpty(name))
            {
                go = GameObject.Find(name);
            }

            return go;
        }

        internal static float[] ToFloatArray(Vector3 v) => new[] { v.x, v.y, v.z };
    }
}
