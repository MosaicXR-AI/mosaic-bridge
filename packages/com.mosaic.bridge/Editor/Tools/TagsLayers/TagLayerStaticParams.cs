using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.TagsLayers
{
    public sealed class TagLayerStaticParams
    {
        [Required] public string Action { get; set; } // get, set
        public int? InstanceId { get; set; }          // find GO by instance ID
        public string GameObjectName { get; set; }    // find GO by name fallback
        public string Flags { get; set; }             // comma-separated flags for set action
    }
}
