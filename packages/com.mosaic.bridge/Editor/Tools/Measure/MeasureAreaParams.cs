namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Parameters for measure/area. Provide either Polygon (list of [x,y,z] vertices)
    /// or GameObjectName (a scene object with a MeshFilter). One is required.
    /// </summary>
    public sealed class MeasureAreaParams
    {
        /// <summary>Ordered polygon vertices [[x,y,z], ...]. Triangulated via fan from vertex 0.</summary>
        public float[][] Polygon { get; set; }

        /// <summary>Name of a scene GameObject with a MeshFilter; area = sum of triangle areas.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Area unit: "m2" (default), "cm2", "ft2", "in2".</summary>
        public string Unit { get; set; } = "m2";

        /// <summary>If true, creates a filled polygon LineRenderer/MeshRenderer visual in the scene.</summary>
        public bool CreateVisual { get; set; } = false;

        /// <summary>Fill color [r,g,b,a]. Defaults to semi-transparent green [0, 1, 0, 0.3].</summary>
        public float[] FillColor { get; set; }
    }
}
