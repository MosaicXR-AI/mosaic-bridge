namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsNBodyResult
    {
        public string ScriptPath     { get; set; }
        public string GameObjectName { get; set; }
        public int    InstanceId     { get; set; }
        public int    BodyCount      { get; set; }
        public string Integrator     { get; set; }
        public float  Theta          { get; set; }
    }
}
