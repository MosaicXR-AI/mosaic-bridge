namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenCellularAutomataResult
    {
        public string ComputeShaderPath { get; set; }
        public string ManagerScriptPath { get; set; }
        public int    Width             { get; set; }
        public int    Height            { get; set; }
        public string BirthRule         { get; set; }
        public string SurvivalRule      { get; set; }
    }
}
