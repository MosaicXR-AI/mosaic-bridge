namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>Result for measure/bounds tool.</summary>
    public sealed class MeasureBoundsResult
    {
        public float[] Min { get; set; }
        public float[] Max { get; set; }
        public float[] Center { get; set; }
        public float[] Size { get; set; }
        /// <summary>Half-size (Size * 0.5).</summary>
        public float[] Extents { get; set; }
        /// <summary>Volume = Size.x * Size.y * Size.z (in output unit cubed).</summary>
        public float Volume { get; set; }
        /// <summary>6-face surface area = 2*(x*y + y*z + x*z) (in output unit squared).</summary>
        public float SurfaceArea { get; set; }
        /// <summary>Space diagonal length = sqrt(x^2 + y^2 + z^2).</summary>
        public float DiagonalLength { get; set; }
        public string Unit { get; set; }
        public string Mode { get; set; }
        /// <summary>-1 if no visual created, otherwise the wireframe GameObject's InstanceID.</summary>
        public int AnnotationId { get; set; } = -1;
    }
}
