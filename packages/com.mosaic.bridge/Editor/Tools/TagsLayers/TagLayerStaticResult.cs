namespace Mosaic.Bridge.Tools.TagsLayers
{
    public sealed class TagLayerStaticResult
    {
        public string GameObjectName { get; set; }
        public string Flags { get; set; }  // human-readable flags string
        public int RawValue { get; set; }  // raw int value of the flags
    }
}
