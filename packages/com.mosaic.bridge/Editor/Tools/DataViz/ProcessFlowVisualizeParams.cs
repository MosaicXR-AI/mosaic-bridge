using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.DataViz
{
    public sealed class ProcessFlowVisualizeParams
    {
        /// <summary>List of flows to visualize (required, must be non-empty).</summary>
        public List<Flow> Flows { get; set; }

        /// <summary>Particles per second along each flow (default 10).</summary>
        public float ParticleRate { get; set; } = 10f;

        /// <summary>Particle speed in world units / sec (default 2).</summary>
        public float ParticleSpeed { get; set; } = 2f;

        /// <summary>Particle start size (default 0.1).</summary>
        public float ParticleSize { get; set; } = 0.1f;

        /// <summary>Flow color (RGBA, default cyan [0,1,1,1]).</summary>
        public float[] FlowColor { get; set; }

        /// <summary>If true, color particles by flow Value via a gradient.</summary>
        public bool ColorByValue { get; set; } = false;

        /// <summary>Optional name override for the flow visualization root GameObject.</summary>
        public string Name { get; set; }
    }

    public sealed class Flow
    {
        public string From { get; set; }
        public string To { get; set; }
        public float Value { get; set; } = 1f;
    }
}
