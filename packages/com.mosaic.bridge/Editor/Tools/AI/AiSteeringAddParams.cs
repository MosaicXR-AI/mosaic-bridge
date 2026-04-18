using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiSteeringAddParams
    {
        /// <summary>Name of the target GameObject to add steering behaviors to.</summary>
        [Required] public string GameObjectName { get; set; }

        /// <summary>List of steering behaviors to add.</summary>
        [Required] public List<SteeringBehavior> Behaviors { get; set; }

        /// <summary>Maximum speed of the agent. Defaults to 5.</summary>
        public float? MaxSpeed { get; set; }

        /// <summary>Maximum steering force applied per frame. Defaults to 10.</summary>
        public float? MaxForce { get; set; }

        /// <summary>Mass of the agent (affects acceleration). Defaults to 1.</summary>
        public float? Mass { get; set; }

        /// <summary>Radius for detecting neighbor agents (flocking behaviors). Defaults to 10.</summary>
        public float? NeighborRadius { get; set; }
    }

    public sealed class SteeringBehavior
    {
        /// <summary>Behavior type: seek, flee, arrive, wander, pursue, evade, obstacle_avoidance, separation, alignment, cohesion, path_follow, leader_follow.</summary>
        [Required] public string Type { get; set; }

        /// <summary>Weight multiplier for this behavior in the combined steering force. Defaults to 1.0.</summary>
        public float? Weight { get; set; }

        /// <summary>Name of the target GameObject for this behavior (e.g. seek target, leader).</summary>
        public string Target { get; set; }

        /// <summary>Radius parameter used by arrive (deceleration), wander (circle), etc.</summary>
        public float? Radius { get; set; }
    }
}
