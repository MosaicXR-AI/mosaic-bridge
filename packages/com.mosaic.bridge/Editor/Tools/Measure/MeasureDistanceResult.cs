namespace Mosaic.Bridge.Tools.Measure
{
    public sealed class MeasureDistanceResult
    {
        public float Distance { get; set; }
        public string Unit { get; set; }
        public float[] FromPoint { get; set; }
        public float[] ToPoint { get; set; }
        public int AnnotationId { get; set; } = -1;
        public string AnnotationName { get; set; }
    }
}
