namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenCaveParams
    {
        public int?    Width            { get; set; }
        public int?    Height           { get; set; }
        public int?    FillPercent      { get; set; }
        public int?    SmoothIterations { get; set; }
        public int?    Seed             { get; set; }
        public string  OutputDirectory  { get; set; }
    }
}
