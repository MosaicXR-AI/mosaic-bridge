namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerSetValueResult
    {
        public string Operation     { get; set; }
        public string MixerName     { get; set; }
        public string SnapshotName  { get; set; }
        public string GroupName     { get; set; }
        public string ParamKind     { get; set; }
        public float  Value         { get; set; }
        public string Message       { get; set; }
    }
}
