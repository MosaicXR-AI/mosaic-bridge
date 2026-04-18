namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenHydraulicErodeParams
    {
        public int?    MapSize                  { get; set; }
        public int?    NumDroplets              { get; set; }
        public int?    ErosionRadius            { get; set; }
        public float?  Inertia                  { get; set; }
        public float?  SedimentCapacityFactor   { get; set; }
        public float?  MinSedimentCapacity      { get; set; }
        public float?  DepositSpeed             { get; set; }
        public float?  ErodeSpeed               { get; set; }
        public float?  EvaporateSpeed           { get; set; }
        public float?  Gravity                  { get; set; }
        public int?    MaxDropletLifetime       { get; set; }
        public string  OutputDirectory          { get; set; }
    }
}
