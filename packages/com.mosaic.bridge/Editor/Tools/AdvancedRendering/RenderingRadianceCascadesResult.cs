namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class RenderingRadianceCascadesResult
    {
        public string ScriptPath        { get; set; }
        public string ComputeShaderPath { get; set; }
        public int    CascadeCount      { get; set; }
        public string Pipeline          { get; set; }
    }
}
