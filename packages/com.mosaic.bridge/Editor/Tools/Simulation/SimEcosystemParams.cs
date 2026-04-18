namespace Mosaic.Bridge.Tools.Simulation
{
    public sealed class SimEcosystemParams
    {
        public int?    PreyCount             { get; set; }
        public int?    PredatorCount         { get; set; }
        public float?  PreyReproductionRate  { get; set; }
        public float?  PredatorDeathRate     { get; set; }
        public float?  CatchRadius           { get; set; }
        public float?  WorldSize             { get; set; }
        public int?    MaxPopulation         { get; set; }
        public string  OutputDirectory       { get; set; }
    }
}
