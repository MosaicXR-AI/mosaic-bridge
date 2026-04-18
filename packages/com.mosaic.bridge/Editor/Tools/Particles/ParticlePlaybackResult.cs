namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticlePlaybackResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string Action { get; set; }
        public bool IsPlaying { get; set; }
        public int ParticleCount { get; set; }
    }
}
