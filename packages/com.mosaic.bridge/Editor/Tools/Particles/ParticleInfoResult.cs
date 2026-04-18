using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleInfoResult
    {
        public List<ParticleInfoEntry> ParticleSystems { get; set; }
        public int TotalCount { get; set; }
    }

    public sealed class ParticleInfoEntry
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public bool IsPlaying { get; set; }
        public int ParticleCount { get; set; }

        // Main module
        public float Duration { get; set; }
        public bool Loop { get; set; }
        public float StartLifetime { get; set; }
        public float StartSpeed { get; set; }
        public float StartSize { get; set; }
        public float[] StartColor { get; set; }
        public float GravityModifier { get; set; }
        public int MaxParticles { get; set; }
        public string SimulationSpace { get; set; }

        // Emission
        public float EmissionRateOverTime { get; set; }
        public int BurstCount { get; set; }

        // Shape
        public string Shape { get; set; }
        public float ShapeRadius { get; set; }
        public float ShapeAngle { get; set; }
    }
}
