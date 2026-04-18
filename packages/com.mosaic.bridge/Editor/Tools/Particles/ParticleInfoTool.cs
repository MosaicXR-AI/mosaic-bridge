using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Particles
{
    public static class ParticleInfoTool
    {
        [MosaicTool("particle/info",
                    "Queries ParticleSystem properties. Specify InstanceId or Name for one, or omit both for all in scene",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<ParticleInfoResult> Execute(ParticleInfoParams p)
        {
            var entries = new List<ParticleInfoEntry>();

            if (p.InstanceId.HasValue || !string.IsNullOrEmpty(p.Name))
            {
                // Query a specific particle system
                var ps = ParticleToolHelpers.Resolve(p.InstanceId, p.Name);
                if (ps == null)
                    return ToolResult<ParticleInfoResult>.Fail(
                        $"ParticleSystem not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                        ErrorCodes.NOT_FOUND);
                entries.Add(BuildEntry(ps));
            }
            else
            {
                // Query all particle systems in the scene
                var all = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
                foreach (var ps in all)
                    entries.Add(BuildEntry(ps));
            }

            return ToolResult<ParticleInfoResult>.Ok(new ParticleInfoResult
            {
                ParticleSystems = entries,
                TotalCount      = entries.Count
            });
        }

        private static ParticleInfoEntry BuildEntry(ParticleSystem ps)
        {
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;

            return new ParticleInfoEntry
            {
                InstanceId        = ps.gameObject.GetInstanceID(),
                Name              = ps.gameObject.name,
                HierarchyPath     = ParticleToolHelpers.GetHierarchyPath(ps.transform),
                IsPlaying         = ps.isPlaying,
                ParticleCount     = ps.particleCount,
                Duration          = main.duration,
                Loop              = main.loop,
                StartLifetime     = main.startLifetime.constant,
                StartSpeed        = main.startSpeed.constant,
                StartSize         = main.startSize.constant,
                StartColor        = new[]
                {
                    main.startColor.color.r,
                    main.startColor.color.g,
                    main.startColor.color.b,
                    main.startColor.color.a
                },
                GravityModifier   = main.gravityModifier.constant,
                MaxParticles      = main.maxParticles,
                SimulationSpace   = main.simulationSpace.ToString(),
                EmissionRateOverTime = emission.rateOverTime.constant,
                BurstCount        = emission.burstCount,
                Shape             = shape.shapeType.ToString(),
                ShapeRadius       = shape.radius,
                ShapeAngle        = shape.angle
            };
        }
    }
}
