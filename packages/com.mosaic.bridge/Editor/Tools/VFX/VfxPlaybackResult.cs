namespace Mosaic.Bridge.Tools.VFX
{
    public sealed class VfxPlaybackResult
    {
        public string GameObjectName    { get; set; }
        public int    InstanceId        { get; set; }
        public string Action            { get; set; }
        public int    AliveParticleCount { get; set; }
        public string Message           { get; set; }
    }
}
