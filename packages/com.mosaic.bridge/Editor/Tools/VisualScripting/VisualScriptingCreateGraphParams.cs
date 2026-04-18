#if MOSAIC_HAS_VISUALSCRIPTING
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.VisualScripting
{
    public sealed class VisualScriptingCreateGraphParams
    {
        /// <summary>Asset path for the new Script Graph (e.g. "Assets/Graphs/MyGraph.asset").</summary>
        [Required] public string Path { get; set; }

        /// <summary>Display name of the graph.</summary>
        [Required] public string Name { get; set; }

        /// <summary>Optional: name of a GameObject to attach the graph to via a ScriptMachine component.</summary>
        public string AttachTo { get; set; }
    }
}
#endif
