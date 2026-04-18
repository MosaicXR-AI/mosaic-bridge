namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class ShaderCreateComputeResult
    {
        public string ComputeShaderPath { get; set; }
        public string KernelName        { get; set; }
        public int    ThreadGroupSize   { get; set; }
        public string BufferType        { get; set; }
    }
}
