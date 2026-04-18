using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.DataViz
{
    public sealed class ProcessStateSetParams
    {
        /// <summary>Name of the GameObject whose process state should be set (required).</summary>
        public string TargetGameObject { get; set; }

        /// <summary>State identifier (required), e.g. "running", "idle", "fault", "maintenance", "offline".</summary>
        public string State { get; set; }

        /// <summary>Display mode: "color" (default), "emission", "icon", "combined".</summary>
        public string DisplayMode { get; set; } = "color";

        /// <summary>Optional list of state definitions overriding the built-in defaults.</summary>
        public List<StateDef> StateConfig { get; set; }

        /// <summary>If true, briefly pulses (blinks) the target's color for ~2s on state change.</summary>
        public bool BlinkOnChange { get; set; } = false;

        /// <summary>If true, applies the visual to all child renderers in the hierarchy.</summary>
        public bool Propagate { get; set; } = false;

        /// <summary>Optional name override (currently informational; used in result).</summary>
        public string Name { get; set; }
    }

    public sealed class StateDef
    {
        public string State { get; set; }
        public float[] Color { get; set; }
        public string IconText { get; set; }
        public string Description { get; set; }
    }
}
