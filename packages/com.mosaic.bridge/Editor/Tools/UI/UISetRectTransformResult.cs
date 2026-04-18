namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UISetRectTransformResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public float[] AnchorMin { get; set; }
        public float[] AnchorMax { get; set; }
        public float[] Pivot { get; set; }
        public float[] SizeDelta { get; set; }
        public float[] AnchoredPosition { get; set; }
    }
}
