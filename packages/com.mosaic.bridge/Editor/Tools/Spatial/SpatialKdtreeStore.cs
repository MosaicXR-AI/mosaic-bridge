using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Spatial
{
    /// <summary>
    /// Shared in-memory storage for KD-tree instances keyed by StructureId.
    /// Lives in the Editor domain; cleared on domain reload.
    /// </summary>
    internal static class SpatialKdtreeStore
    {
        internal sealed class StoredPoint
        {
            public string  Id;
            public float[] Position;
            public string  Data;
        }

        internal sealed class KDNode
        {
            public StoredPoint Point;
            public int         Axis;
            public KDNode      Left;
            public KDNode      Right;
        }

        internal sealed class KDTree
        {
            public int    Dimensions;
            public int    PointCount;
            public int    Depth;
            public KDNode Root;
        }

        internal static readonly Dictionary<string, KDTree> Trees =
            new Dictionary<string, KDTree>();
    }
}
