using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiUtilityCreateParams
    {
        /// <summary>Name of the utility AI agent (used as class prefix).</summary>
        [Required] public string AgentName { get; set; }

        /// <summary>List of actions the agent can choose from.</summary>
        [Required] public List<UtilityAction> Actions { get; set; }

        /// <summary>Input axes that feed into consideration curves.</summary>
        public List<InputDef> Inputs { get; set; }

        /// <summary>Optional GameObject to attach the generated component to.</summary>
        public string AttachTo { get; set; }

        /// <summary>Asset path for the generated script. Defaults to "Assets/Generated/AI/".</summary>
        public string SavePath { get; set; }
    }

    public sealed class UtilityAction
    {
        /// <summary>Display name of this action.</summary>
        [Required] public string Name { get; set; }

        /// <summary>Considerations that score this action.</summary>
        public List<Consideration> Considerations { get; set; }

        /// <summary>How to combine consideration scores: "multiply", "average", "min".</summary>
        public string CombinationMethod { get; set; }

        /// <summary>Weight multiplier for the final score. Defaults to 1.</summary>
        public float Weight { get; set; } = 1f;

        /// <summary>Method name to call when this action is selected.</summary>
        public string MethodName { get; set; }
    }

    public sealed class Consideration
    {
        /// <summary>Name of the input axis to evaluate.</summary>
        public string InputAxis { get; set; }

        /// <summary>Response curve type: "linear", "quadratic", "logistic", "exponential", "step".</summary>
        public string Curve { get; set; }

        /// <summary>Slope parameter for the curve. Defaults to 1.</summary>
        public float Slope { get; set; } = 1f;

        /// <summary>Exponent parameter for quadratic/exponential curves. Defaults to 2.</summary>
        public float Exponent { get; set; } = 2f;

        /// <summary>Horizontal shift applied to input before curve evaluation. Defaults to 0.</summary>
        public float Shift { get; set; } = 0f;

        /// <summary>Threshold for step curve. Defaults to 0.5.</summary>
        public float Threshold { get; set; } = 0.5f;
    }

    public sealed class InputDef
    {
        /// <summary>Name of this input axis (becomes a public float field).</summary>
        public string Name { get; set; }

        /// <summary>Semantic type hint: "float", "health", "distance", "ammo", "time".</summary>
        public string Type { get; set; }

        /// <summary>Description of where this value comes from.</summary>
        public string Source { get; set; }
    }
}
