namespace Mosaic.Bridge.Tools.DataViz
{
    public sealed class ProcessFlowVisualizeResult
    {
        /// <summary>Name of the flow visualization root GameObject.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Number of flows successfully created.</summary>
        public int FlowCount { get; set; }

        /// <summary>Number of active ParticleSystems instantiated.</summary>
        public int ActiveParticleSystems { get; set; }
    }
}
