namespace Mosaic.Bridge.Tools.Renderers
{
    public sealed class RendererLineResult
    {
        public string GameObjectName { get; set; }
        public int    InstanceId     { get; set; }
        public int    PositionCount  { get; set; }
        public bool   Loop           { get; set; }
        public bool   ComponentAdded { get; set; }
        public string Message        { get; set; }
    }
}
