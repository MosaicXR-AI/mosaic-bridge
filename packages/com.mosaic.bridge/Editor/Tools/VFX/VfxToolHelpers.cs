using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.VFX
{
    internal static class VfxToolHelpers
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

        /// <summary>VFX Graph requires compute shaders and structured buffers, which WebGL does not
        /// support (docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/
        /// System-Requirements.html). Checked against the CURRENT active build target as a
        /// best-effort proxy — a course's declared target platform list is not visible from here.</summary>
        internal static bool IsActiveBuildTargetWebGL() =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;
    }
}
