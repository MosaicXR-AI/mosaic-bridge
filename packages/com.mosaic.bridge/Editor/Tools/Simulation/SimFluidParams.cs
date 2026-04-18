namespace Mosaic.Bridge.Tools.Simulation
{
    public sealed class SimFluidParams
    {
        public int?    ParticleCount   { get; set; }
        public float?  FluidDensity    { get; set; }
        public float?  Viscosity       { get; set; }
        public float?  Gravity         { get; set; }
        public float?  SmoothingRadius { get; set; }
        public float?  BoundarySize    { get; set; }
        public string  OutputDirectory { get; set; }
    }
}
