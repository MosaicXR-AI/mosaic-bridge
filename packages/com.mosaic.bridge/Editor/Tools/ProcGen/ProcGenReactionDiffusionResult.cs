namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenReactionDiffusionResult
    {
        public string ComputeShaderPath { get; set; }
        public string ManagerScriptPath { get; set; }
        public int    Width             { get; set; }
        public int    Height            { get; set; }
        public float  FeedRate          { get; set; }
        public float  KillRate          { get; set; }
    }
}
