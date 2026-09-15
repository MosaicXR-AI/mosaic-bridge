using UnityEngine;
using UnityEngine.VFX;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.VFX
{
    public static class VfxCreateTool
    {
        // O4 §4.8 (G4): using shipped/Asset-Store VFX assets in HDRP/URP courses; E35 point clouds.
        // VisualEffect/VisualEffectAsset are core-engine types, but a VisualEffect with no asset is
        // inert — this exists mainly to wire up an already-authored .vfx asset onto a GameObject.
        // VFX Graph requires compute + SSBO, unsupported on WebGL — refused up front.
        [MosaicTool("vfx/create",
                    "Creates a GameObject with a VisualEffect component, optionally assigning a " +
                    "VisualEffectAsset (.vfx graph) by AssetPath. Refuses when the active build target " +
                    "is WebGL (VFX Graph needs compute shaders/SSBO, unsupported there).",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<VfxCreateResult> Execute(VfxCreateParams p)
        {
            if (VfxToolHelpers.IsActiveBuildTargetWebGL())
                return ToolResult<VfxCreateResult>.Fail(
                    "VFX Graph requires compute shaders and structured buffers, which WebGL does not support.",
                    ErrorCodes.NOT_PERMITTED);

            GameObject parent = null;
            if (!string.IsNullOrEmpty(p.ParentName))
            {
                parent = GameObject.Find(p.ParentName);
                if (parent == null)
                    return ToolResult<VfxCreateResult>.Fail(
                        $"ParentName GameObject '{p.ParentName}' not found", ErrorCodes.NOT_FOUND);
            }

            var go = new GameObject(p.Name);
            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create Visual Effect");
            if (parent != null) go.transform.SetParent(parent.transform, false);
            if (p.Position != null)
            {
                if (p.Position.Length != 3)
                    return ToolResult<VfxCreateResult>.Fail("Position requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                go.transform.position = new Vector3(p.Position[0], p.Position[1], p.Position[2]);
            }

            var vfx = go.AddComponent<VisualEffect>();

            string assetPath = null;
            if (!string.IsNullOrEmpty(p.AssetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(p.AssetPath);
                if (asset == null)
                    return ToolResult<VfxCreateResult>.Fail(
                        $"VisualEffectAsset not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);
                vfx.visualEffectAsset = asset;
                assetPath = p.AssetPath;
            }

            return ToolResult<VfxCreateResult>.Ok(new VfxCreateResult
            {
                GameObjectName = go.name,
                InstanceId     = UnityIds.Of(go),
                AssetPath      = assetPath,
                Message        = assetPath != null
                    ? $"Created '{go.name}' with VisualEffect using '{assetPath}'."
                    : $"Created '{go.name}' with an empty VisualEffect (no AssetPath given — it won't play anything until one is assigned).",
            });
        }
    }
}
