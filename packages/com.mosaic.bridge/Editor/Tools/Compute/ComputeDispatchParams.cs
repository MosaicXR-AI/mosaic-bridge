using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Compute
{
    public sealed class ComputeDispatchParams
    {
        [Required] public string ShaderPath { get; set; }
        public string KernelName { get; set; } = "CSMain";
        public int ThreadGroupsX { get; set; } = 1;
        public int ThreadGroupsY { get; set; } = 1;
        public int ThreadGroupsZ { get; set; } = 1;
        public float[] BufferData { get; set; }
    }
}
