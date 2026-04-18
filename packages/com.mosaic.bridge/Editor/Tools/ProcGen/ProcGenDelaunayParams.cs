using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenDelaunayParams
    {
        [Required] public float[][] Points     { get; set; }
        public bool?                CreateMesh { get; set; }
        public float?               MeshHeight { get; set; }
        public string               SavePath   { get; set; }
    }
}
