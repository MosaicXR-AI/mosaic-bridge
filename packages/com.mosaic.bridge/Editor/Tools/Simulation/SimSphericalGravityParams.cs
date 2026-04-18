namespace Mosaic.Bridge.Tools.Simulation
{
    public sealed class SimSphericalGravityParams
    {
        public float?  GravityStrength { get; set; }
        public float?  PlanetRadius    { get; set; }
        public int?    ObjectCount     { get; set; }
        public string  OutputDirectory { get; set; }
    }
}
