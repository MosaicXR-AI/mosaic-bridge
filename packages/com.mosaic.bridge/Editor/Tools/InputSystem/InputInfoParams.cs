#if MOSAIC_HAS_INPUT_SYSTEM
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.InputSystem
{
    public sealed class InputInfoParams
    {
        [Required] public string AssetPath { get; set; }
    }
}
#endif
