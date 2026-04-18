#if MOSAIC_HAS_TMP
namespace Mosaic.Bridge.Tools.TextMeshPro
{
    public sealed class TmpInfoResult
    {
        public TmpComponentInfo[] Components { get; set; }
    }

    public sealed class TmpComponentInfo
    {
        public string GameObjectName { get; set; }
        public int InstanceId { get; set; }
        public string HierarchyPath { get; set; }
        public string ComponentType { get; set; }
        public string Text { get; set; }
        public string FontName { get; set; }
        public float FontSize { get; set; }
        public float[] Color { get; set; }
        public string Alignment { get; set; }
        public string FontStyle { get; set; }
        public string OverflowMode { get; set; }
        public float[] Bounds { get; set; }
        public int CharacterCount { get; set; }
    }
}
#endif
