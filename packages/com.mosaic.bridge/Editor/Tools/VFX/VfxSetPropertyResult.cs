namespace Mosaic.Bridge.Tools.VFX
{
    public sealed class VfxSetPropertyResult
    {
        public string GameObjectName { get; set; }
        public int    InstanceId     { get; set; }
        public string PropertyName   { get; set; }
        public string ValueType      { get; set; }
        public string Message        { get; set; }
    }
}
