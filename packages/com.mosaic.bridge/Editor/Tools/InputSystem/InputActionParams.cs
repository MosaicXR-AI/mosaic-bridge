#if MOSAIC_HAS_INPUT_SYSTEM
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.InputSystem
{
    public sealed class InputActionParams
    {
        [Required] public string AssetPath { get; set; }
        [Required] public string Action { get; set; }    // "add", "remove", "add-binding", "add-composite"
        [Required] public string MapName { get; set; }
        public string ActionName { get; set; }            // Required for all actions
        public string ActionType { get; set; }            // "Button", "Value", "PassThrough" — for add
        public string BindingPath { get; set; }           // e.g. "<Keyboard>/space" — for add-binding
        public string CompositeName { get; set; }         // e.g. "WASD" — for add-composite
        public string CompositeType { get; set; }         // e.g. "2DVector" — for add-composite
        public string CompositePart { get; set; }         // e.g. "up" — for add-composite
    }
}
#endif
