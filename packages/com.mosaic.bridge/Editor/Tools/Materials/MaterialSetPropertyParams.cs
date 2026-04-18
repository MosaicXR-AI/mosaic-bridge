namespace Mosaic.Bridge.Tools.Materials
{
    public sealed class MaterialSetPropertyParams
    {
        public string  Path         { get; set; }
        public string  Property     { get; set; }
        public string  ValueType    { get; set; }
        public float   FloatValue   { get; set; }
        public float[] ColorValue   { get; set; }
        public float[] VectorValue  { get; set; }
        public int     IntValue     { get; set; }
        public string  TexturePath  { get; set; }
    }
}
