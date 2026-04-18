namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Parameters for the measure/angle tool (Story 33-2).
    /// Computes the angle between two rays emanating from a common vertex.
    /// The three points (vertex, A, B) may be supplied as explicit 3-component
    /// world coordinates or as GameObject names (the object's world position is used).
    /// </summary>
    public sealed class MeasureAngleParams
    {
        /// <summary>Apex of the angle as explicit world coordinates [x, y, z]. Optional.</summary>
        public float[] VertexPoint { get; set; }

        /// <summary>First ray endpoint as explicit world coordinates [x, y, z]. Optional.</summary>
        public float[] PointA { get; set; }

        /// <summary>Second ray endpoint as explicit world coordinates [x, y, z]. Optional.</summary>
        public float[] PointB { get; set; }

        /// <summary>Name of a GameObject whose world position is used as the vertex. Optional.</summary>
        public string VertexGameObject { get; set; }

        /// <summary>Name of a GameObject whose world position is used as point A. Optional.</summary>
        public string GameObjectA { get; set; }

        /// <summary>Name of a GameObject whose world position is used as point B. Optional.</summary>
        public string GameObjectB { get; set; }

        /// <summary>Output unit: "degrees" (default) or "radians".</summary>
        public string Unit { get; set; }

        /// <summary>
        /// If true, creates an arc + label annotation in the scene visualising the angle.
        /// Default: false.
        /// </summary>
        public bool? CreateVisual { get; set; }

        /// <summary>Visual color as RGBA [r, g, b, a]. Default: [1, 1, 0, 1].</summary>
        public float[] VisualColor { get; set; }

        /// <summary>Radius of the visual arc in world units. Default: 0.5.</summary>
        public float? ArcRadius { get; set; }

        /// <summary>Optional name assigned to the created annotation GameObject.</summary>
        public string Name { get; set; }
    }
}
