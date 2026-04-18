namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenCellularAutomataParams
    {
        public int?    Width           { get; set; }
        public int?    Height          { get; set; }
        public string  BirthRule       { get; set; }
        public string  SurvivalRule    { get; set; }
        public float?  InitialDensity  { get; set; }
        public int?    Seed            { get; set; }
        public string  OutputDirectory { get; set; }
    }
}
