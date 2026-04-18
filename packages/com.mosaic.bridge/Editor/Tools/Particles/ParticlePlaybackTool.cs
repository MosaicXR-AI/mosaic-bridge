using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Particles
{
    public static class ParticlePlaybackTool
    {
        [MosaicTool("particle/playback",
                    "Controls playback of a ParticleSystem (play, pause, stop, restart)",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ParticlePlaybackResult> Execute(ParticlePlaybackParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<ParticlePlaybackResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            var ps = ParticleToolHelpers.Resolve(p.InstanceId, p.Name);
            if (ps == null)
                return ToolResult<ParticlePlaybackResult>.Fail(
                    $"ParticleSystem not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                    ErrorCodes.NOT_FOUND);

            string action = (p.Action ?? "").ToLowerInvariant();
            switch (action)
            {
                case "play":
                    ps.Play(withChildren: true);
                    break;
                case "pause":
                    ps.Pause(withChildren: true);
                    break;
                case "stop":
                    ps.Stop(withChildren: true);
                    ps.Clear(withChildren: true);
                    break;
                case "restart":
                    ps.Stop(withChildren: true);
                    ps.Clear(withChildren: true);
                    ps.Play(withChildren: true);
                    break;
                default:
                    return ToolResult<ParticlePlaybackResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid values: play, pause, stop, restart",
                        ErrorCodes.INVALID_PARAM);
            }

            return ToolResult<ParticlePlaybackResult>.Ok(new ParticlePlaybackResult
            {
                InstanceId    = ps.gameObject.GetInstanceID(),
                Name          = ps.gameObject.name,
                Action        = action,
                IsPlaying     = ps.isPlaying,
                ParticleCount = ps.particleCount
            });
        }
    }
}
