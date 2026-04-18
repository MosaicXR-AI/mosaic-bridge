using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// Parameters for the chart/scatter tool (Story 34-4).
    /// Renders a 3D scatter plot as a parent GameObject containing one sphere per point
    /// plus optional axes, grid, and labels.
    /// </summary>
    public sealed class ChartScatterParams
    {
        /// <summary>A single scatter-plot point.</summary>
        public sealed class Point
        {
            /// <summary>Data-space XYZ position of the point. Required.</summary>
            public float[] Position { get; set; }

            /// <summary>Sphere diameter. Default: 0.1.</summary>
            public float? Size { get; set; }

            /// <summary>Optional RGBA color (0-1).</summary>
            public float[] Color { get; set; }

            /// <summary>Optional text label rendered at the point when ShowLabels is true.</summary>
            public string Label { get; set; }
        }

        /// <summary>List of scatter points. Required, must be non-empty.</summary>
        public List<Point> Points { get; set; }

        /// <summary>Optional axis labels [X, Y, Z]. Default: ["X", "Y", "Z"].</summary>
        public string[] AxisLabels { get; set; }

        /// <summary>If true, draw X/Y/Z axis lines. Default: true.</summary>
        public bool? ShowAxes { get; set; }

        /// <summary>If true, draw a reference grid. Default: false.</summary>
        public bool? ShowGrid { get; set; }

        /// <summary>If true, render per-point text labels. Default: false.</summary>
        public bool? ShowLabels { get; set; }

        /// <summary>If true, auto-scale points to fit in a 10x10x10 cube. Default: true.</summary>
        public bool? AutoScale { get; set; }

        /// <summary>Optional world-space origin of the chart.</summary>
        public float[] Position { get; set; }

        /// <summary>Optional parent GameObject name.</summary>
        public string Name { get; set; }
    }
}
