namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenSdfGenerateParams
    {
        public string  Source           { get; set; }
        public string  PrimitiveType    { get; set; }
        public float[] PrimitiveSize    { get; set; }
        public string  MeshPath         { get; set; }
        public string  Expression       { get; set; }
        public int?    Resolution       { get; set; }
        public float[] BoundsMin        { get; set; }
        public float[] BoundsMax        { get; set; }
        public string  Operation        { get; set; }
        public string  OperandMeshPath  { get; set; }
        public string  OperandPrimitive { get; set; }
        public float[] OperandPrimitiveSize { get; set; }
        public float?  BlendFactor      { get; set; }
        public string  OutputName       { get; set; }
        public string  SavePath         { get; set; }
    }
}
