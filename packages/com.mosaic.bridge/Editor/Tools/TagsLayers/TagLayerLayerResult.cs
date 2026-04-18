namespace Mosaic.Bridge.Tools.TagsLayers
{
    public sealed class TagLayerLayerResult
    {
        public LayerEntry[] Layers { get; set; }    // list action
        public string GameObjectName { get; set; }  // set action
        public int AssignedLayerIndex { get; set; }  // set action
        public string AssignedLayerName { get; set; } // set action
    }

    public sealed class LayerEntry
    {
        public int Index { get; set; }
        public string Name { get; set; }
    }
}
