using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenPoissonDiskParams
    {
        public float[]  BoundsMin     { get; set; }
        [Required] public float[]  BoundsMax     { get; set; }
        [Required] public float    MinDistance    { get; set; }
        public int?     MaxSamples    { get; set; }
        public int?     Seed          { get; set; }
        public int?     Dimensions    { get; set; }
        public string   SurfaceMode   { get; set; }
        public string   SurfaceObject { get; set; }
        public string   PrefabPath    { get; set; }
        public string   ParentObject  { get; set; }
    }
}
