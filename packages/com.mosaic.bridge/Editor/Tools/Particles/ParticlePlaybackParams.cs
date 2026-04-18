using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Particles
{
    public sealed class ParticlePlaybackParams
    {
        public int? InstanceId { get; set; }
        public string Name { get; set; }

        [Required] public string Action { get; set; }  // play, pause, stop, restart
    }
}
