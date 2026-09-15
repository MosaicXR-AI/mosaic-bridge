#if MOSAIC_HAS_CINEMACHINE
namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineCreateManagerResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string ManagerType { get; set; }
        public int ChildCount { get; set; }
        public int InstructionCount { get; set; }
    }
}
#endif
