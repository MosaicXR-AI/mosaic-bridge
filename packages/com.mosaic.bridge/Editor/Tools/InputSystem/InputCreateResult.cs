#if MOSAIC_HAS_INPUT_SYSTEM
namespace Mosaic.Bridge.Tools.InputSystem
{
    public sealed class InputCreateResult
    {
        public string Name { get; set; }
        public string AssetPath { get; set; }
        public string Guid { get; set; }
    }
}
#endif
