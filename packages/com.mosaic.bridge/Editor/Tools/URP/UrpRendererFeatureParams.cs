#if MOSAIC_HAS_URP
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.URP
{
    public sealed class UrpRendererFeatureParams
    {
        /// <summary>Action to perform: "add", "remove", or "list".</summary>
        [Required] public string Action { get; set; }

        /// <summary>Full type name of the renderer feature (e.g. "UnityEngine.Rendering.Universal.DecalRendererFeature"). Required for add/remove.</summary>
        public string FeatureType { get; set; }

        /// <summary>Display name for the feature. Used when adding.</summary>
        public string Name { get; set; }
    }
}
#endif
