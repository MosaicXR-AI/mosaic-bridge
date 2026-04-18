using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiUtilityEvaluateParams
    {
        /// <summary>Name of the GameObject with a UtilityAI component.</summary>
        [Required] public string GameObjectName { get; set; }

        /// <summary>Optional input overrides to set before evaluation.</summary>
        public List<InputOverride> InputOverrides { get; set; }
    }

    public sealed class InputOverride
    {
        /// <summary>Name of the input axis to override.</summary>
        public string Name { get; set; }

        /// <summary>Value to set (0-1 normalized).</summary>
        public float Value { get; set; }
    }
}
