using UnityEngine.VFX;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.VFX
{
    public static class VfxPlaybackTool
    {
        [MosaicTool("vfx/playback",
                    "Controls a VisualEffect's playback: play, stop, reinit, or event (SendEvent by name).",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<VfxPlaybackResult> Execute(VfxPlaybackParams p)
        {
            var go = VfxToolHelpers.ResolveGameObject(p.InstanceId, p.Name);
            if (go == null)
                return ToolResult<VfxPlaybackResult>.Fail(
                    $"GameObject '{p.Name ?? p.InstanceId?.ToString()}' not found", ErrorCodes.NOT_FOUND);

            var vfx = go.GetComponent<VisualEffect>();
            if (vfx == null)
                return ToolResult<VfxPlaybackResult>.Fail(
                    $"No VisualEffect component on '{go.name}'", ErrorCodes.NOT_FOUND);

            switch (p.Action?.ToLowerInvariant())
            {
                case "play": vfx.Play(); break;
                case "stop": vfx.Stop(); break;
                case "reinit": vfx.Reinit(); break;
                case "event":
                    if (string.IsNullOrEmpty(p.EventName))
                        return ToolResult<VfxPlaybackResult>.Fail("EventName is required for 'event'", ErrorCodes.INVALID_PARAM);
                    vfx.SendEvent(p.EventName);
                    break;
                default:
                    return ToolResult<VfxPlaybackResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: play, stop, reinit, event", ErrorCodes.INVALID_PARAM);
            }

            return ToolResult<VfxPlaybackResult>.Ok(new VfxPlaybackResult
            {
                GameObjectName     = go.name,
                InstanceId         = UnityIds.Of(go),
                Action             = p.Action,
                AliveParticleCount = vfx.aliveParticleCount,
                Message            = $"{p.Action} on '{go.name}'" + (p.Action?.ToLowerInvariant() == "event" ? $" ('{p.EventName}')." : "."),
            });
        }
    }
}
