namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleSetMainResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public float Duration { get; set; }
        public float StartLifetime { get; set; }
        public float StartSpeed { get; set; }
        public float StartSize { get; set; }
        public float[] StartColor { get; set; }
        public float GravityModifier { get; set; }
        public int MaxParticles { get; set; }
        public string SimulationSpace { get; set; }
    }
}
