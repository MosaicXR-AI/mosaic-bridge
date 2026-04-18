namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public sealed class NavFOVVisualizeResult
    {
        public int   GameObjectInstanceId { get; set; }
        public float ViewAngle            { get; set; }
        public float ViewRadius           { get; set; }
        public int   HitCount             { get; set; }
        public int   MeshVertexCount      { get; set; }
    }
}
