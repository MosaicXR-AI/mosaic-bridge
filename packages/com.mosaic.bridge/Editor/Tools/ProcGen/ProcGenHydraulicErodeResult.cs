namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenHydraulicErodeResult
    {
        public string ComputeShaderPath { get; set; }
        public string ManagerScriptPath { get; set; }
        public int    MapSize           { get; set; }
        public int    NumDroplets       { get; set; }
    }
}
