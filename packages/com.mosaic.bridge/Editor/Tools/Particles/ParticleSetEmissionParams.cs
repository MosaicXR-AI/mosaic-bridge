namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticleSetEmissionParams
    {
        public int? InstanceId { get; set; }
        public string Name { get; set; }

        public float? RateOverTime { get; set; }
        public BurstEntry[] Bursts { get; set; }
    }

    public sealed class BurstEntry
    {
        public float Time { get; set; }
        public int Count { get; set; }
    }
}
