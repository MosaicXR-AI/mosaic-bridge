namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Parameters for the analysis/solar tool (Story 33-7).
    /// Computes sun position (elevation, azimuth, direction) for a given latitude,
    /// longitude, date and time using a simplified NOAA solar-position algorithm.
    /// Optionally produces scene visualization (sun marker, arc, shadow) and
    /// shadow-length analysis for a target GameObject.
    /// </summary>
    public sealed class AnalysisSolarParams
    {
        /// <summary>Latitude in degrees (positive = North). Default: 40.0 (NYC).</summary>
        public float? Latitude { get; set; }

        /// <summary>Longitude in degrees (positive = East). Default: -74.0 (NYC).</summary>
        public float? Longitude { get; set; }

        /// <summary>Date in "yyyy-MM-dd" format. Default: today.</summary>
        public string Date { get; set; }

        /// <summary>Local time of day in hours (0-24). Default: 12.0.</summary>
        public float? TimeOfDay { get; set; }

        /// <summary>Time zone offset from UTC in hours. Default: -5.0 (US Eastern).</summary>
        public float? TimeZone { get; set; }

        /// <summary>Scene direction considered "North". Default: [0, 0, 1] (+Z).</summary>
        public float[] SceneNorth { get; set; }

        /// <summary>If true, renders sun path / marker / shadow in the scene. Default: false.</summary>
        public bool? CreateVisual { get; set; }

        /// <summary>Optional GameObject name to analyze shadow length for.</summary>
        public string TargetGameObject { get; set; }

        /// <summary>
        /// Analysis type: "current_position", "full_day_arc", "year_arc", or
        /// "shadow_at_time". Default: "current_position".
        /// </summary>
        public string AnalysisType { get; set; }

        /// <summary>Number of samples to use for arc visualizations. Default: 24.</summary>
        public int? SampleCount { get; set; }
    }
}
