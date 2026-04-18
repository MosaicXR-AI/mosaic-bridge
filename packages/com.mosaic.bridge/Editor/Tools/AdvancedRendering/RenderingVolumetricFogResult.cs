namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class RenderingVolumetricFogResult
    {
        public string ScriptPath        { get; set; }
        public string ComputeShaderPath { get; set; }
        public string Pipeline          { get; set; }
        public int[]  Resolution        { get; set; }
    }
}
