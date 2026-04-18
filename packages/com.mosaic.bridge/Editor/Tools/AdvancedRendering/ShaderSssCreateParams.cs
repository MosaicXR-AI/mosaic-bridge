namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class ShaderSssCreateParams
    {
        public string  Profile          { get; set; }
        public float?  ScatterDistance  { get; set; }
        public float?  Thickness        { get; set; }
        public float[] ScatterColor     { get; set; }
        public string  AlbedoPath       { get; set; }
        public string  ThicknessMapPath { get; set; }
        public string  NormalPath       { get; set; }
        public string  Pipeline         { get; set; }
        public string  OutputName       { get; set; }
        public string  SavePath         { get; set; }
    }
}
