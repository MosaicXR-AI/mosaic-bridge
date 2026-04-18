#if MOSAIC_HAS_ADDRESSABLES
namespace Mosaic.Bridge.Tools.Addressables
{
    public sealed class AddressablesGroupsResult
    {
        public string Action { get; set; }
        public GroupInfo[] Groups { get; set; }
        public string Message { get; set; }
    }

    public sealed class GroupInfo
    {
        public string Name { get; set; }
        public int EntryCount { get; set; }
        public bool IsDefault { get; set; }
        public bool IsReadOnly { get; set; }
    }
}
#endif
