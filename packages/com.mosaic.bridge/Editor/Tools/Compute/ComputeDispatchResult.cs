namespace Mosaic.Bridge.Tools.Compute
{
    public sealed class ComputeDispatchResult
    {
        public string KernelName { get; set; }
        public string ThreadGroups { get; set; }
        public int BufferSize { get; set; }
        public float[] OutputData { get; set; }
        public float ExecutionTimeMs { get; set; }
    }
}
