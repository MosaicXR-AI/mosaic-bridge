using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// Parameters for the data/graph-layout tool (Story 34-5).
    /// Computes 2D/3D node positions for a graph using a chosen layout algorithm
    /// (force-directed Fruchterman-Reingold, circular, grid, or spring) and optionally
    /// spawns node spheres + edge LineRenderers under a parent GameObject.
    /// </summary>
    public sealed class DataGraphLayoutParams
    {
        /// <summary>A single graph node.</summary>
        public sealed class Node
        {
            /// <summary>Unique node identifier. Required.</summary>
            public string Id { get; set; }

            /// <summary>Optional display label for the node.</summary>
            public string Label { get; set; }

            /// <summary>Optional weight (reserved — does not currently change layout). Default 1.</summary>
            public float? Weight { get; set; }
        }

        /// <summary>A single graph edge.</summary>
        public sealed class Edge
        {
            /// <summary>Source node Id. Required.</summary>
            public string From { get; set; }

            /// <summary>Target node Id. Required.</summary>
            public string To { get; set; }

            /// <summary>Edge spring weight multiplier. Default 1.</summary>
            public float? Weight { get; set; }
        }

        /// <summary>Graph nodes. Must be non-empty; Ids must be unique.</summary>
        public List<Node> Nodes { get; set; }

        /// <summary>Graph edges. Each From/To must reference a known node Id.</summary>
        public List<Edge> Edges { get; set; }

        /// <summary>Layout algorithm: "force_directed" (default), "circular", "grid", "spring".</summary>
        public string Algorithm { get; set; }

        /// <summary>Number of force/spring iterations. Default 200. Ignored for circular/grid.</summary>
        public int? Iterations { get; set; }

        /// <summary>Ideal resting edge length. Default 2.0.</summary>
        public float? IdealEdgeLength { get; set; }

        /// <summary>Strength of node-node repulsion. Default 1.0.</summary>
        public float? Repulsion { get; set; }

        /// <summary>Edge spring attraction strength. Default 0.1.</summary>
        public float? Attraction { get; set; }

        /// <summary>Velocity damping per iteration. Default 0.85.</summary>
        public float? Damping { get; set; }

        /// <summary>Layout bounding box half-sizes [x, y, z]. Default [10, 10, 10].</summary>
        public float[] Bounds { get; set; }

        /// <summary>If true (default), use full 3D; otherwise flat on XZ (Y=0).</summary>
        public bool? Layout3D { get; set; }

        /// <summary>Optional deterministic seed for random initialization.</summary>
        public int? Seed { get; set; }

        /// <summary>If true (default), spawn node/edge GameObjects under a parent.</summary>
        public bool? CreateVisuals { get; set; }

        /// <summary>Sphere node visual scale. Default 0.3.</summary>
        public float? NodeSize { get; set; }

        /// <summary>LineRenderer edge thickness. Default 0.03.</summary>
        public float? EdgeThickness { get; set; }

        /// <summary>RGBA node color. Default [0.3, 0.7, 1, 1].</summary>
        public float[] NodeColor { get; set; }

        /// <summary>RGBA edge color. Default [0.7, 0.7, 0.7, 0.6].</summary>
        public float[] EdgeColor { get; set; }

        /// <summary>Optional parent GameObject name. Default "GraphLayout".</summary>
        public string Name { get; set; }
    }
}
