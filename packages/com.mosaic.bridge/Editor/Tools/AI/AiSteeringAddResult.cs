namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiSteeringAddResult
    {
        /// <summary>Name of the GameObject the steering agent was added to.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Instance ID of the GameObject.</summary>
        public int InstanceId { get; set; }

        /// <summary>Number of steering behaviors configured.</summary>
        public int BehaviorCount { get; set; }

        /// <summary>Maximum speed of the agent.</summary>
        public float MaxSpeed { get; set; }

        /// <summary>Array of behavior type names that were added.</summary>
        public string[] Behaviors { get; set; }

        /// <summary>Path to the generated script asset.</summary>
        public string ScriptPath { get; set; }
    }
}
