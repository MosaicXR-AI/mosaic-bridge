namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioSetSpatialResult
    {
        public int    InstanceId               { get; set; }
        public string GameObjectName           { get; set; }
        public float  MinDistance              { get; set; }
        public float  MaxDistance              { get; set; }
        public string RolloffMode              { get; set; }
        public float  DopplerLevel             { get; set; }
        public float  Spread                   { get; set; }
        public string NoAudioListenerWarning   { get; set; }
    }
}
