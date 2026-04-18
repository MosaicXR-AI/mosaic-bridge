#if MOSAIC_HAS_URP
namespace Mosaic.Bridge.Tools.URP
{
    public sealed class UrpDecalResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public float[] Size { get; set; }
        public float[] Position { get; set; }
        public float[] Rotation { get; set; }
        public string MaterialName { get; set; }
    }
}
#endif
