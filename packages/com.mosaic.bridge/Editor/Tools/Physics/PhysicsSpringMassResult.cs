namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsSpringMassResult
    {
        public string ScriptPath      { get; set; }
        public string GameObjectName  { get; set; }
        public int    InstanceId      { get; set; }
        public string Preset          { get; set; }
        public int    ParticleCount   { get; set; }
        public int    SpringCount     { get; set; }
    }
}
