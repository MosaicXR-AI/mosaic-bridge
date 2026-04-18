namespace Mosaic.Bridge.Tools.Simulation
{
    public sealed class SimOrbitalParams
    {
        public int?    BodyCount              { get; set; }
        public float?  GravitationalConstant  { get; set; }
        public float?  TimeStep               { get; set; }
        public float?  Softening              { get; set; }
        public float?  CentralMass            { get; set; }
        public float?  InitialRadius          { get; set; }
        public string  OutputDirectory        { get; set; }
    }
}
