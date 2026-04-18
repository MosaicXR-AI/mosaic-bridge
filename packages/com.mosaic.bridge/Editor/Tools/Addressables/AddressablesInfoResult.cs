#if MOSAIC_HAS_ADDRESSABLES
namespace Mosaic.Bridge.Tools.Addressables
{
    public sealed class AddressablesInfoResult
    {
        public int TotalGroups { get; set; }
        public int TotalEntries { get; set; }
        public string[] AllLabels { get; set; }
        public string ActiveProfileName { get; set; }
        public string BuildPath { get; set; }
        public string LoadPath { get; set; }
        public AddressablesGroupDetail[] Groups { get; set; }
    }

    public sealed class AddressablesGroupDetail
    {
        public string Name { get; set; }
        public bool IsDefault { get; set; }
        public AddressablesEntryInfo[] Entries { get; set; }
    }

    public sealed class AddressablesEntryInfo
    {
        public string Address { get; set; }
        public string AssetPath { get; set; }
        public string Guid { get; set; }
        public string[] Labels { get; set; }
    }
}
#endif
