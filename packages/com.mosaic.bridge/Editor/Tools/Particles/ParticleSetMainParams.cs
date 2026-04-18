namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleSetMainParams
    {
        public int? InstanceId { get; set; }
        public string Name { get; set; }

        public float? Duration { get; set; }
        public float? StartLifetime { get; set; }
        public float? StartSpeed { get; set; }
        public float? StartSize { get; set; }
        public float[] StartColor { get; set; }      // [r,g,b,a] 0-1 range
        public float? GravityModifier { get; set; }
        public int? MaxParticles { get; set; }
        public string SimulationSpace { get; set; }   // "Local" or "World"
    }
}
