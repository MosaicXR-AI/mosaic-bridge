#if MOSAIC_HAS_INPUT_SYSTEM
using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.InputSystem
{
    public sealed class InputMapResult
    {
        public string AssetPath { get; set; }
        public string Action { get; set; }
        public string MapName { get; set; }
        public List<string> Maps { get; set; }
    }
}
#endif
