using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.VFX
{
    public sealed class VfxCreateParams
    {
        /// <summary>Name for the new GameObject.</summary>
        [Required] public string Name { get; set; }

        /// <summary>Asset path of a VisualEffectAsset (.vfx graph) to assign. Required to actually
        /// play anything — a VisualEffect with no asset is inert.</summary>
        public string AssetPath { get; set; }

        /// <summary>[x,y,z] world position. Defaults to origin.</summary>
        public float[] Position { get; set; }

        /// <summary>Name of a GameObject to parent this under.</summary>
        public string ParentName { get; set; }
    }
}
