namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsVerletCreateResult
    {
        public string ScriptPath     { get; set; }
        public string GameObjectName { get; set; }
        public int    InstanceId     { get; set; }
        public string Type           { get; set; }
        public int    PointCount     { get; set; }
    }
}
