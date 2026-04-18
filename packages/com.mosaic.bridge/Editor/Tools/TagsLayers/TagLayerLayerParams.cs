using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.TagsLayers
{
    public sealed class TagLayerLayerParams
    {
        [Required] public string Action { get; set; } // list, set
        public string LayerName { get; set; }         // set by name
        public int? LayerIndex { get; set; }          // set by index (0-31)
        public int? InstanceId { get; set; }          // find GO by instance ID (set)
        public string GameObjectName { get; set; }    // find GO by name fallback (set)
    }
}
