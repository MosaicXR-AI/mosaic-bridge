namespace Mosaic.Bridge.Tools.VFX
{
    public sealed class VfxInfoResult
    {
        public string GameObjectName      { get; set; }
        public int    InstanceId          { get; set; }
        public string AssetPath           { get; set; }
        public bool   HasAsset            { get; set; }
        public int    AliveParticleCount  { get; set; }
        public bool   Paused              { get; set; }
        public float  PlayRate            { get; set; }
        public string[] SystemNames       { get; set; }
    }
}
