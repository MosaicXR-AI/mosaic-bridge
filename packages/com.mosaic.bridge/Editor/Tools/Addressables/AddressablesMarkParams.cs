#if MOSAIC_HAS_ADDRESSABLES
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Addressables
{
    public sealed class AddressablesMarkParams
    {
        /// <summary>Path to the asset to mark as addressable (relative to project, e.g. "Assets/Prefabs/Player.prefab").</summary>
        [Required] public string AssetPath { get; set; }

        /// <summary>Addressable group name. If null, uses the default group.</summary>
        public string Group { get; set; }

        /// <summary>Labels to apply to the entry.</summary>
        public string[] Labels { get; set; }

        /// <summary>Custom address for the entry. Defaults to the asset path if null.</summary>
        public string Address { get; set; }
    }
}
#endif
