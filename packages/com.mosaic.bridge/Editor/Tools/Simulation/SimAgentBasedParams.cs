namespace Mosaic.Bridge.Tools.Simulation
{
    public sealed class SimAgentBasedParams
    {
        public int?    AgentCount      { get; set; }
        public float?  SensorAngle     { get; set; }
        public float?  SensorDistance   { get; set; }
        public float?  TurnSpeed       { get; set; }
        public float?  MoveSpeed       { get; set; }
        public float?  TrailWeight     { get; set; }
        public float?  DecayRate       { get; set; }
        public int?    Resolution      { get; set; }
        public string  OutputDirectory { get; set; }
    }
}
