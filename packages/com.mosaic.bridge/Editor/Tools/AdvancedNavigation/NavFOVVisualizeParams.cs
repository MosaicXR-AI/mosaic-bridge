namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public sealed class NavFOVVisualizeParams
    {
        public int?    OriginInstanceId { get; set; }
        public string  OriginName       { get; set; }
        public float?  ViewAngle        { get; set; }
        public float?  ViewRadius       { get; set; }
        public int?    RayCount         { get; set; }
        public string  ObstacleMask     { get; set; }
        public bool    CreateMesh       { get; set; } = true;
    }
}
