namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Result envelope for the measure/angle tool (Story 33-2).
    /// </summary>
    public sealed class MeasureAngleResult
    {
        /// <summary>Computed angle in the requested unit.</summary>
        public float Angle { get; set; }

        /// <summary>Unit of the Angle field ("degrees" or "radians").</summary>
        public string Unit { get; set; }

        /// <summary>Vertex (apex) world coordinates [x, y, z].</summary>
        public float[] Vertex { get; set; }

        /// <summary>Normalized direction from vertex toward point A.</summary>
        public float[] RayA { get; set; }

        /// <summary>Normalized direction from vertex toward point B.</summary>
        public float[] RayB { get; set; }

        /// <summary>
        /// Instance ID of the annotation GameObject if a visual was created, -1 otherwise.
        /// </summary>
        public int AnnotationId { get; set; }

        /// <summary>Name of the created annotation GameObject (null if no visual).</summary>
        public string AnnotationName { get; set; }
    }
}
