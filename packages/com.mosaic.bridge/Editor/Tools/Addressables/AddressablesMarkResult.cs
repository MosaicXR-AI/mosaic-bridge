#if MOSAIC_HAS_ADDRESSABLES
namespace Mosaic.Bridge.Tools.Addressables
{
    public sealed class AddressablesMarkResult
    {
        public string Address { get; set; }
        public string GroupName { get; set; }
        public string[] LabelsApplied { get; set; }
        public string AssetPath { get; set; }
        public string Guid { get; set; }
    }
}
#endif
