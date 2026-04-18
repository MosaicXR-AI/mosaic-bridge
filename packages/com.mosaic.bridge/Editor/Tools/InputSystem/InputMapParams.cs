#if MOSAIC_HAS_INPUT_SYSTEM
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.InputSystem
{
    public sealed class InputMapParams
    {
        [Required] public string AssetPath { get; set; }
        [Required] public string Action { get; set; } // "add", "remove", "list"
        public string MapName { get; set; }            // Required for add/remove
    }
}
#endif
