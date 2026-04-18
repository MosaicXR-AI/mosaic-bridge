namespace Mosaic.Bridge.Tools.Simulation
{
    public sealed class SimSmokeParams
    {
        public int?    Resolution     { get; set; }
        public float?  Density        { get; set; }
        public float?  Temperature    { get; set; }
        public float?  BuoyancyForce  { get; set; }
        public float?  DiffusionRate  { get; set; }
        public float?  TimeStep       { get; set; }
        public string  OutputDirectory { get; set; }
    }
}
