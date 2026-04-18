namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenTerrainResult
    {
        public string ComputeShaderPath { get; set; }
        public string ManagerScriptPath { get; set; }
        public int    Width             { get; set; }
        public int    Depth             { get; set; }
        public int    Octaves           { get; set; }
    }
}
