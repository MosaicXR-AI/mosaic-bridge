namespace Mosaic.Bridge.Tools.Measure
{
    public sealed class AnnotationCreateResult
    {
        public int AnnotationId { get; set; }
        public string GameObjectName { get; set; }
        public string Type { get; set; }
        public float[] Position { get; set; }
    }
}
