using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// Parameters for the chart/network tool (Story 34-4).
    /// Renders a 3D graph (nodes + edges). When nodes have null positions and
    /// AutoLayout is true, a simple force-directed layout is computed.
    /// </summary>
    public sealed class ChartNetworkParams
    {
        /// <summary>A graph node.</summary>
        public sealed class Node
        {
            /// <summary>Unique node identifier referenced by edges. Required.</summary>
            public string Id { get; set; }

            /// <summary>Optional XYZ position. If null and AutoLayout is true, auto-computed.</summary>
            public float[] Position { get; set; }

            /// <summary>Sphere diameter. Default: 0.5.</summary>
            public float? Size { get; set; }

            /// <summary>Optional RGBA color (0-1).</summary>
            public float[] Color { get; set; }

            /// <summary>Display label shown above the node.</summary>
            public string Label { get; set; }
        }

        /// <summary>A directed/undirected edge.</summary>
        public sealed class Edge
        {
            /// <summary>Source node Id. Required.</summary>
            public string From { get; set; }

            /// <summary>Target node Id. Required.</summary>
            public string To { get; set; }

            /// <summary>Edge weight (controls line thickness). Default: 1.</summary>
            public float? Weight { get; set; }

            /// <summary>Optional RGBA color (0-1).</summary>
            public float[] Color { get; set; }
        }

        /// <summary>List of nodes. Required, must be non-empty.</summary>
        public List<Node> Nodes { get; set; }

        /// <summary>List of edges.</summary>
        public List<Edge> Edges { get; set; }

        /// <summary>If true and node positions are missing, run a simple force-directed layout. Default: true.</summary>
        public bool? AutoLayout { get; set; }

        /// <summary>Force-directed layout iteration count. Default: 100.</summary>
        public int? LayoutIterations { get; set; }

        /// <summary>Optional world-space origin of the chart.</summary>
        public float[] Position { get; set; }

        /// <summary>Optional parent GameObject name.</summary>
        public string Name { get; set; }
    }
}
