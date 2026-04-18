using System;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Particles
{
    public static class ParticleSetMainTool
    {
        [MosaicTool("particle/set-main",
                    "Sets main module properties on a ParticleSystem",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ParticleSetMainResult> Execute(ParticleSetMainParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<ParticleSetMainResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            var ps = ParticleToolHelpers.Resolve(p.InstanceId, p.Name);
            if (ps == null)
                return ToolResult<ParticleSetMainResult>.Fail(
                    $"ParticleSystem not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(ps, "Mosaic: Set ParticleSystem Main");

            var main = ps.main;

            if (p.Duration.HasValue)
                main.duration = p.Duration.Value;
            if (p.StartLifetime.HasValue)
                main.startLifetime = new ParticleSystem.MinMaxCurve(p.StartLifetime.Value);
            if (p.StartSpeed.HasValue)
                main.startSpeed = new ParticleSystem.MinMaxCurve(p.StartSpeed.Value);
            if (p.StartSize.HasValue)
                main.startSize = new ParticleSystem.MinMaxCurve(p.StartSize.Value);
            if (p.StartColor != null && p.StartColor.Length >= 3)
            {
                float a = p.StartColor.Length >= 4 ? p.StartColor[3] : 1f;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(p.StartColor[0], p.StartColor[1], p.StartColor[2], a));
            }
            if (p.GravityModifier.HasValue)
                main.gravityModifier = p.GravityModifier.Value;
            if (p.MaxParticles.HasValue)
                main.maxParticles = p.MaxParticles.Value;
            if (!string.IsNullOrEmpty(p.SimulationSpace))
            {
                if (Enum.TryParse<ParticleSystemSimulationSpace>(p.SimulationSpace, true, out var space))
                    main.simulationSpace = space;
            }

            EditorUtility.SetDirty(ps);

            return ToolResult<ParticleSetMainResult>.Ok(new ParticleSetMainResult
            {
                InstanceId      = ps.gameObject.GetInstanceID(),
                Name            = ps.gameObject.name,
                Duration        = main.duration,
                StartLifetime   = main.startLifetime.constant,
                StartSpeed      = main.startSpeed.constant,
                StartSize       = main.startSize.constant,
                StartColor      = new[]
                {
                    main.startColor.color.r,
                    main.startColor.color.g,
                    main.startColor.color.b,
                    main.startColor.color.a
                },
                GravityModifier = main.gravityModifier.constant,
                MaxParticles    = main.maxParticles,
                SimulationSpace = main.simulationSpace.ToString()
            });
        }
    }
}
