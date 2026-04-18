using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiContextSteeringParams
    {
        /// <summary>Name of the target GameObject to add context steering to.</summary>
        [Required] public string GameObjectName { get; set; }

        /// <summary>Interest sources that attract the agent (targets or directions).</summary>
        [Required] public List<InterestSource> InterestSources { get; set; }

        /// <summary>Danger sources that repel the agent (obstacles, agents, zones).</summary>
        public List<DangerSource> DangerSources { get; set; }

        /// <summary>Number of direction slots around the agent (evenly spaced). Defaults to 16.</summary>
        public int? Resolution { get; set; }

        /// <summary>Maximum movement speed. Defaults to 5.</summary>
        public float? MaxSpeed { get; set; }

        /// <summary>Optional script class name suffix. Defaults to sanitized GO name.</summary>
        public string Name { get; set; }

        /// <summary>Output directory for the generated script. Defaults to "Assets/Generated/AI/".</summary>
        public string SavePath { get; set; }
    }

    public sealed class InterestSource
    {
        /// <summary>Source type: "target" (GameObject name) or "direction" (Vector3 as "x,y,z").</summary>
        [Required] public string Type { get; set; }

        /// <summary>GameObject name for "target", or "x,y,z" string for "direction".</summary>
        [Required] public string Value { get; set; }

        /// <summary>Weight multiplier for this source. Defaults to 1.0.</summary>
        public float? Weight { get; set; }
    }

    public sealed class DangerSource
    {
        /// <summary>Source type: "obstacle" (GO name), "agent" (GO name), or "zone" (GO name with collider).</summary>
        [Required] public string Type { get; set; }

        /// <summary>GameObject name of the danger source.</summary>
        [Required] public string Value { get; set; }

        /// <summary>Weight multiplier for this source. Defaults to 1.0.</summary>
        public float? Weight { get; set; }

        /// <summary>Radius within which the danger source affects the agent. Defaults to 5.0.</summary>
        public float? Radius { get; set; }
    }
}
