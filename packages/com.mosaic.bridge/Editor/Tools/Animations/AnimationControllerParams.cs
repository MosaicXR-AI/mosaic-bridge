using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationControllerParams
    {
        /// <summary>Action to perform: create, info, add-parameter, remove-parameter, add-layer</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path for the controller (e.g. "Assets/Animations/MyController.controller")</summary>
        public string Path { get; set; }

        // -- create --
        // Uses Path only

        // -- add-parameter / remove-parameter --
        /// <summary>Parameter name (for add-parameter)</summary>
        public string ParameterName { get; set; }

        /// <summary>Parameter type: Float, Int, Bool, Trigger (for add-parameter)</summary>
        public string ParameterType { get; set; }

        /// <summary>Parameter index (for remove-parameter)</summary>
        public int? ParameterIndex { get; set; }

        // -- add-layer --
        /// <summary>Layer name (for add-layer)</summary>
        public string LayerName { get; set; }
    }
}
