namespace Mosaic.Bridge.Tools.Compute
{
    public sealed class ComputeCreateResult
    {
        public string ComputeShaderPath { get; set; }
        public string ManagerScriptPath { get; set; }
        public string Template { get; set; }
        public string[] KernelNames { get; set; }
    }
}
