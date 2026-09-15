using UnityEngine.VFX;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.VFX
{
    public static class VfxInfoTool
    {
        [MosaicTool("vfx/info",
                    "Reports a VisualEffect's assigned asset, alive particle count, pause/play-rate state, " +
                    "and its graph's system names.",
                    isReadOnly: true)]
        public static ToolResult<VfxInfoResult> Execute(VfxInfoParams p)
        {
            var go = VfxToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<VfxInfoResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            var vfx = go.GetComponent<VisualEffect>();
            if (vfx == null)
                return ToolResult<VfxInfoResult>.Fail(
                    $"No VisualEffect component on '{go.name}'", ErrorCodes.NOT_FOUND);

            var systemNames = new System.Collections.Generic.List<string>();
            vfx.GetSystemNames(systemNames);

            return ToolResult<VfxInfoResult>.Ok(new VfxInfoResult
            {
                GameObjectName     = go.name,
                InstanceId         = UnityIds.Of(go),
                AssetPath          = vfx.visualEffectAsset != null ? AssetDatabase.GetAssetPath(vfx.visualEffectAsset) : null,
                HasAsset           = vfx.visualEffectAsset != null,
                AliveParticleCount = vfx.aliveParticleCount,
                Paused             = vfx.pause,
                PlayRate           = vfx.playRate,
                SystemNames        = systemNames.ToArray(),
            });
        }
    }
}
