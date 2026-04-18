#if MOSAIC_HAS_INPUT_SYSTEM
using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.InputSystem
{
    public sealed class InputActionResult
    {
        public string AssetPath { get; set; }
        public string Action { get; set; }
        public string MapName { get; set; }
        public string ActionName { get; set; }
        public List<string> Actions { get; set; }
        public List<string> Bindings { get; set; }
    }
}
#endif
