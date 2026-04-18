namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Result envelope for the analysis/solar tool (Story 33-7).
    /// </summary>
    public sealed class AnalysisSolarResult
    {
        /// <summary>Normalized direction in scene space pointing from origin toward the sun.</summary>
        public float[] SunDirection { get; set; }

        /// <summary>Sun elevation above horizon in degrees (0-90 while above horizon; negative below).</summary>
        public float SunElevation { get; set; }

        /// <summary>Sun azimuth measured from North, clockwise, in degrees (0-360).</summary>
        public float SunAzimuth { get; set; }

        /// <summary>True if the sun is above the horizon at the requested time.</summary>
        public bool IsDaytime { get; set; }

        /// <summary>Local sunrise time in "HH:mm" format (empty if polar day/night).</summary>
        public string Sunrise { get; set; }

        /// <summary>Local sunset time in "HH:mm" format (empty if polar day/night).</summary>
        public string Sunset { get; set; }

        /// <summary>Length of the day (sun above horizon) in hours. 24 = polar day, 0 = polar night.</summary>
        public float DayLength { get; set; }

        /// <summary>Instance ID of the created annotation GameObject, or -1 if no visual.</summary>
        public int AnnotationId { get; set; }

        /// <summary>Shadow length in world units for TargetGameObject (0 if none/sun below horizon).</summary>
        public float ShadowLength { get; set; }
    }
}
