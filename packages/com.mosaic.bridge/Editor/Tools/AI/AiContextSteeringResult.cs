namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiContextSteeringResult
    {
        /// <summary>Name of the GameObject the context steering agent was added to.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Instance ID of the GameObject.</summary>
        public int InstanceId { get; set; }

        /// <summary>Path to the generated script asset.</summary>
        public string ScriptPath { get; set; }

        /// <summary>Number of direction slots (resolution).</summary>
        public int Resolution { get; set; }

        /// <summary>Number of interest sources configured.</summary>
        public int InterestSourceCount { get; set; }

        /// <summary>Number of danger sources configured.</summary>
        public int DangerSourceCount { get; set; }
    }
}
