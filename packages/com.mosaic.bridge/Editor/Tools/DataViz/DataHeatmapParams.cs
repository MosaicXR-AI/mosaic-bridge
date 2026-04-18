using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// Parameters for the data/heatmap tool (Story 34-1).
    /// Bakes a scalar-data heatmap texture onto a target mesh via UV lookup,
    /// producing a material + texture asset and optionally a legend GameObject.
    /// </summary>
    public sealed class DataHeatmapParams
    {
        /// <summary>A single scalar data sample at a world-space position.</summary>
        public sealed class DataPoint
        {
            /// <summary>World-space XYZ position of the sample.</summary>
            public float[] Position { get; set; }

            /// <summary>Scalar value at the sample.</summary>
            public float Value { get; set; }
        }

        /// <summary>Name of the target GameObject in the scene. Required. Must have a MeshFilter.</summary>
        public string TargetObject { get; set; }

        /// <summary>Scattered scalar samples to interpolate across the surface.</summary>
        public List<DataPoint> DataPoints { get; set; }

        /// <summary>Color ramp. Valid: thermal, viridis, jet, coolwarm, grayscale. Default: thermal.</summary>
        public string ColorGradient { get; set; }

        /// <summary>Interpolation mode. Valid: nearest, linear, idw. Default: idw.</summary>
        public string Interpolation { get; set; }

        /// <summary>Power exponent for IDW interpolation. Default: 2.</summary>
        public float? IdwPower { get; set; }

        /// <summary>Optional minimum value clamp. If null, auto-detected from DataPoints.</summary>
        public float? ValueMin { get; set; }

        /// <summary>Optional maximum value clamp. If null, auto-detected from DataPoints.</summary>
        public float? ValueMax { get; set; }

        /// <summary>If true, creates a legend child GameObject with the gradient. Default: false.</summary>
        public bool? ShowLegend { get; set; }

        /// <summary>Baked texture resolution (square). Default: 128.</summary>
        public int? Resolution { get; set; }

        /// <summary>Optional name used for the generated assets.</summary>
        public string Name { get; set; }

        /// <summary>Directory under Assets/. Default: Assets/Generated/DataViz/.</summary>
        public string SavePath { get; set; }
    }
}
