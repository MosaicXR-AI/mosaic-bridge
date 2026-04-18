#if MOSAIC_HAS_VISUALSCRIPTING
namespace Mosaic.Bridge.Tools.VisualScripting
{
    public sealed class VisualScriptingCreateGraphResult
    {
        public string AssetPath { get; set; }
        public string Name { get; set; }
        public string AttachedTo { get; set; }
        public int AttachedInstanceId { get; set; }
    }
}
#endif
