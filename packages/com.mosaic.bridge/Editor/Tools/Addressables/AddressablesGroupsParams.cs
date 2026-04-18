#if MOSAIC_HAS_ADDRESSABLES
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Addressables
{
    public sealed class AddressablesGroupsParams
    {
        /// <summary>Action to perform: "list", "create", or "delete".</summary>
        [Required] public string Action { get; set; }

        /// <summary>Group name (required for create and delete actions).</summary>
        public string GroupName { get; set; }

        /// <summary>When deleting, move entries to the default group instead of removing them. Default true.</summary>
        public bool MoveEntriesToDefault { get; set; } = true;
    }
}
#endif
