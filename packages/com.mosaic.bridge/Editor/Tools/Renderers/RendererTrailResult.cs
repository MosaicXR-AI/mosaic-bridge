namespace Mosaic.Bridge.Tools.Renderers
{
    public sealed class RendererTrailResult
    {
        public string GameObjectName      { get; set; }
        public int    InstanceId          { get; set; }
        public float  Time                { get; set; }
        public float  MinVertexDistance   { get; set; }
        public bool   Emitting            { get; set; }
        public bool   Autodestruct        { get; set; }
        public bool   ComponentAdded      { get; set; }
        public string Message             { get; set; }
    }
}
