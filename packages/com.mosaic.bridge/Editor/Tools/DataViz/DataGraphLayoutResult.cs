using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// Result payload for data/graph-layout (Story 34-5).
    /// </summary>
    public sealed class DataGraphLayoutResult
    {
        /// <summary>One node's computed position.</summary>
        public sealed class NodePosition
        {
            /// <summary>Node identifier.</summary>
            public string Id { get; set; }

            /// <summary>Final world-space position as [x, y, z].</summary>
            public float[] Position { get; set; }
        }

        /// <summary>Layout parent GameObject name (empty when CreateVisuals=false).</summary>
        public string GameObjectName { get; set; }

        /// <summary>Number of nodes in the graph.</summary>
        public int NodeCount { get; set; }

        /// <summary>Number of edges in the graph.</summary>
        public int EdgeCount { get; set; }

        /// <summary>Algorithm used (normalized lower-case).</summary>
        public string Algorithm { get; set; }

        /// <summary>Iterations actually run (0 for circular/grid).</summary>
        public int Iterations { get; set; }

        /// <summary>Final computed positions per node.</summary>
        public List<NodePosition> NodePositions { get; set; }
    }
}
