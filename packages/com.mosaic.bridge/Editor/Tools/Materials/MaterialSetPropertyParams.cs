namespace Mosaic.Bridge.Tools.Materials
{
    public sealed class MaterialSetPropertyParams
    {
        public string  Path         { get; set; }
        public string  Property     { get; set; }

        /// <summary>One of: float, int, color, vector, texture, bool, keyword.
        /// Note: bool supports material-level flags (enableInstancing,
        /// doubleSidedGI) that are NOT shader properties; HasProperty is
        /// bypassed for those.
        /// keyword enables/disables a shader keyword (e.g. _EMISSION, _NORMALMAP).</summary>
        public string  ValueType    { get; set; }
        public float   FloatValue   { get; set; }
        public float[] ColorValue   { get; set; }
        public float[] VectorValue  { get; set; }
        public int     IntValue     { get; set; }
        public string  TexturePath  { get; set; }
        public bool    BoolValue    { get; set; }
    }
}
