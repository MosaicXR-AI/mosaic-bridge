#if MOSAIC_HAS_VISUALSCRIPTING
namespace Mosaic.Bridge.Tools.VisualScripting
{
    public sealed class VisualScriptingAddNodeResult
    {
        public string GraphPath { get; set; }
        public string NodeType { get; set; }
        public float[] Position { get; set; }
        public string NodeDescription { get; set; }
        public int TotalNodeCount { get; set; }
    }
}
#endif
