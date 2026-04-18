using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Particles
{
    public static class ParticleSetEmissionTool
    {
        [MosaicTool("particle/set-emission",
                    "Sets emission module properties on a ParticleSystem",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ParticleSetEmissionResult> Execute(ParticleSetEmissionParams p)
        {
            if (p.InstanceId == null && string.IsNullOrEmpty(p.Name))
                return ToolResult<ParticleSetEmissionResult>.Fail(
                    "Either InstanceId or Name is required", ErrorCodes.INVALID_PARAM);

            var ps = ParticleToolHelpers.Resolve(p.InstanceId, p.Name);
            if (ps == null)
                return ToolResult<ParticleSetEmissionResult>.Fail(
                    $"ParticleSystem not found (InstanceId={p.InstanceId}, Name='{p.Name}')",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(ps, "Mosaic: Set ParticleSystem Emission");

            var emission = ps.emission;

            if (p.RateOverTime.HasValue)
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(p.RateOverTime.Value);

            if (p.Bursts != null && p.Bursts.Length > 0)
            {
                var bursts = new ParticleSystem.Burst[p.Bursts.Length];
                for (int i = 0; i < p.Bursts.Length; i++)
                {
                    bursts[i] = new ParticleSystem.Burst(p.Bursts[i].Time, (short)p.Bursts[i].Count);
                }
                emission.SetBursts(bursts);
            }

            EditorUtility.SetDirty(ps);

            return ToolResult<ParticleSetEmissionResult>.Ok(new ParticleSetEmissionResult
            {
                InstanceId   = ps.gameObject.GetInstanceID(),
                Name         = ps.gameObject.name,
                RateOverTime = emission.rateOverTime.constant,
                BurstCount   = emission.burstCount
            });
        }
    }
}
