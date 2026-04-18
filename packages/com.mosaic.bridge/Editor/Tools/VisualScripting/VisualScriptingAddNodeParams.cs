#if MOSAIC_HAS_VISUALSCRIPTING
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.VisualScripting
{
    public sealed class VisualScriptingAddNodeParams
    {
        /// <summary>Asset path of the Script Graph (e.g. "Assets/Graphs/MyGraph.asset").</summary>
        [Required] public string GraphPath { get; set; }

        /// <summary>
        /// Node type to add. Examples: "Debug.Log", "Transform.Translate",
        /// "UnityEngine.Debug.Log", or a full type name for custom units.
        /// </summary>
        [Required] public string NodeType { get; set; }

        /// <summary>Position on the graph canvas [x, y]. Default [0, 0].</summary>
        public float[] Position { get; set; }
    }
}
#endif
