#if MOSAIC_HAS_ADDRESSABLES
namespace Mosaic.Bridge.Tools.Addressables
{
    public sealed class AddressablesInfoParams
    {
        /// <summary>Optional group name to filter results to a single group.</summary>
        public string Group { get; set; }

        /// <summary>Optional label to filter entries that have this label.</summary>
        public string Label { get; set; }
    }
}
#endif
