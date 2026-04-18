#if MOSAIC_HAS_INPUT_SYSTEM
using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.InputSystem
{
    public sealed class InputInfoResult
    {
        public string AssetPath { get; set; }
        public string Name { get; set; }
        public string Guid { get; set; }
        public List<InputMapInfo> Maps { get; set; }
    }

    public sealed class InputMapInfo
    {
        public string Name { get; set; }
        public List<InputActionInfo> Actions { get; set; }
    }

    public sealed class InputActionInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public List<InputBindingInfo> Bindings { get; set; }
    }

    public sealed class InputBindingInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsComposite { get; set; }
        public bool IsPartOfComposite { get; set; }
    }
}
#endif
