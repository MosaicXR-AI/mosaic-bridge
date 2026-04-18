using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.TagsLayers
{
    public sealed class TagLayerTagParams
    {
        [Required] public string Action { get; set; } // list, add, set
        public string TagName { get; set; }            // required for add, set
        public int? InstanceId { get; set; }           // find GO by instance ID (set)
        public string GameObjectName { get; set; }     // find GO by name fallback (set)
    }
}
