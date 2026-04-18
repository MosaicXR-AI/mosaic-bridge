using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// Parameters for the chart/bar tool (Story 34-4).
    /// Renders a 3D bar chart as a parent GameObject with one cube per bar,
    /// auto-scaled so the largest value matches MaxHeight.
    /// </summary>
    public sealed class ChartBarParams
    {
        /// <summary>A single bar.</summary>
        public sealed class Bar
        {
            /// <summary>Text label shown below the bar when ShowLabels is true.</summary>
            public string Label { get; set; }

            /// <summary>Raw value (height before normalization).</summary>
            public float Value { get; set; }

            /// <summary>Optional RGBA color (0-1).</summary>
            public float[] Color { get; set; }
        }

        /// <summary>List of bars. Required, must be non-empty.</summary>
        public List<Bar> Bars { get; set; }

        /// <summary>Bar width/depth. Default: 0.8.</summary>
        public float? BarWidth { get; set; }

        /// <summary>Center-to-center spacing between bars along X. Default: 1.0.</summary>
        public float? BarSpacing { get; set; }

        /// <summary>Maximum rendered height; values auto-scaled to this. Default: 10.</summary>
        public float? MaxHeight { get; set; }

        /// <summary>If true, draw X/Y axis lines. Default: true.</summary>
        public bool? ShowAxes { get; set; }

        /// <summary>If true, render per-bar text labels. Default: true.</summary>
        public bool? ShowLabels { get; set; }

        /// <summary>Optional world-space origin of the chart.</summary>
        public float[] Position { get; set; }

        /// <summary>Optional parent GameObject name.</summary>
        public string Name { get; set; }
    }
}
