#if MOSAIC_HAS_INPUT_SYSTEM
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.InputSystem
{
    public sealed class InputCreateParams
    {
        [Required] public string Name { get; set; }
        [Required] public string Path { get; set; }
    }
}
#endif
