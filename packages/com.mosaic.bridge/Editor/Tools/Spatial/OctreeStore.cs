using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mosaic.Bridge.Tools.Spatial
{
    /// <summary>
    /// Stored point inside the octree/quadtree.
    /// </summary>
    public sealed class OctreePoint
    {
        public string  Id;
        public Vector3 Position;
        public string  Data;
    }

    /// <summary>
    /// Recursive octree (3D) / quadtree (2D) node. Children count is 8 for 3D
    /// and 4 for 2D. Octant indexing (3D): bit0=+x, bit1=+y, bit2=+z relative
    /// to the node center. Quadrant (2D): bit0=+x, bit1=+y.
    /// </summary>
    public sealed class OctreeNode
    {
        public Vector3           BoundsMin;
        public Vector3           BoundsMax;
        public Vector3           Center;
        public int               Depth;
        public int               Dimensions;         // 2 or 3
        public List<OctreePoint> Points;             // null when subdivided
        public OctreeNode[]      Children;           // null when leaf

        public OctreeNode(Vector3 min, Vector3 max, int depth, int dimensions)
        {
            BoundsMin  = min;
            BoundsMax  = max;
            Center     = (min + max) * 0.5f;
            Depth      = depth;
            Dimensions = dimensions;
            Points     = new List<OctreePoint>();
            Children   = null;
        }

        public bool IsLeaf => Children == null;
        public int  ChildCount => Dimensions == 2 ? 4 : 8;
    }

    /// <summary>
    /// Metadata for an octree, including the runtime structure config.
    /// </summary>
    public sealed class OctreeStructure
    {
        public OctreeNode Root;
        public int        Dimensions;
        public int        MaxDepth;
        public int        MaxPointsPerNode;
        public int        PointCount;
        public int        NodeCount;
        public int        MaxDepthReached;
    }

    /// <summary>
    /// Process-wide static store keyed by StructureId. Persists across tool calls
    /// for the lifetime of the Editor domain.
    /// </summary>
    public static class OctreeStore
    {
        private static readonly Dictionary<string, OctreeStructure> _structures
            = new Dictionary<string, OctreeStructure>();

        public static void Put(string id, OctreeStructure s) => _structures[id] = s;

        public static bool TryGet(string id, out OctreeStructure s)
            => _structures.TryGetValue(id, out s);

        public static bool Contains(string id) => _structures.ContainsKey(id);

        public static bool Remove(string id)   => _structures.Remove(id);

        public static void Clear() => _structures.Clear();

        // ─────────────────────────────────────────────────────────────────
        // Insertion
        // ─────────────────────────────────────────────────────────────────
        public static void Insert(OctreeStructure s, OctreePoint point)
        {
            s.PointCount++;
            InsertInto(s, s.Root, point);
        }

        private static void InsertInto(OctreeStructure s, OctreeNode node, OctreePoint point)
        {
            if (node.Depth > s.MaxDepthReached)
                s.MaxDepthReached = node.Depth;

            if (!node.IsLeaf)
            {
                int idx = GetChildIndex(node, point.Position);
                InsertInto(s, node.Children[idx], point);
                return;
            }

            node.Points.Add(point);

            if (node.Points.Count > s.MaxPointsPerNode && node.Depth < s.MaxDepth)
                Subdivide(s, node);
        }

        private static void Subdivide(OctreeStructure s, OctreeNode node)
        {
            int childCount = node.ChildCount;
            node.Children = new OctreeNode[childCount];

            Vector3 min = node.BoundsMin;
            Vector3 max = node.BoundsMax;
            Vector3 c   = node.Center;

            for (int i = 0; i < childCount; i++)
            {
                Vector3 cMin = min;
                Vector3 cMax = max;

                cMin.x = (i & 1) != 0 ? c.x : min.x;
                cMax.x = (i & 1) != 0 ? max.x : c.x;

                cMin.y = (i & 2) != 0 ? c.y : min.y;
                cMax.y = (i & 2) != 0 ? max.y : c.y;

                if (s.Dimensions == 3)
                {
                    cMin.z = (i & 4) != 0 ? c.z : min.z;
                    cMax.z = (i & 4) != 0 ? max.z : c.z;
                }
                else
                {
                    cMin.z = min.z;
                    cMax.z = max.z;
                }

                node.Children[i] = new OctreeNode(cMin, cMax, node.Depth + 1, s.Dimensions);
                s.NodeCount++;
                if (node.Depth + 1 > s.MaxDepthReached)
                    s.MaxDepthReached = node.Depth + 1;
            }

            var toRedistribute = node.Points;
            node.Points = null;

            foreach (var p in toRedistribute)
            {
                int idx = GetChildIndex(node, p.Position);
                // Redistribution must NOT increment PointCount or recurse the
                // "incoming" bookkeeping — just place the point in the child,
                // which may itself subdivide.
                InsertRedistribute(s, node.Children[idx], p);
            }
        }

        private static void InsertRedistribute(OctreeStructure s, OctreeNode node, OctreePoint p)
        {
            if (!node.IsLeaf)
            {
                int idx = GetChildIndex(node, p.Position);
                InsertRedistribute(s, node.Children[idx], p);
                return;
            }

            node.Points.Add(p);
            if (node.Points.Count > s.MaxPointsPerNode && node.Depth < s.MaxDepth)
                Subdivide(s, node);
        }

        private static int GetChildIndex(OctreeNode node, Vector3 p)
        {
            int idx = 0;
            if (p.x >= node.Center.x) idx |= 1;
            if (p.y >= node.Center.y) idx |= 2;
            if (node.Dimensions == 3 && p.z >= node.Center.z) idx |= 4;
            return idx;
        }

        // ─────────────────────────────────────────────────────────────────
        // Bounds helpers
        // ─────────────────────────────────────────────────────────────────
        public static bool Contains(OctreeNode node, Vector3 p)
        {
            if (p.x < node.BoundsMin.x || p.x > node.BoundsMax.x) return false;
            if (p.y < node.BoundsMin.y || p.y > node.BoundsMax.y) return false;
            if (node.Dimensions == 3 && (p.z < node.BoundsMin.z || p.z > node.BoundsMax.z))
                return false;
            return true;
        }

        public static bool BoundsIntersectRange(OctreeNode node, Vector3 rMin, Vector3 rMax, int dims)
        {
            if (node.BoundsMax.x < rMin.x || node.BoundsMin.x > rMax.x) return false;
            if (node.BoundsMax.y < rMin.y || node.BoundsMin.y > rMax.y) return false;
            if (dims == 3 && (node.BoundsMax.z < rMin.z || node.BoundsMin.z > rMax.z)) return false;
            return true;
        }

        public static bool PointInRange(Vector3 p, Vector3 rMin, Vector3 rMax, int dims)
        {
            if (p.x < rMin.x || p.x > rMax.x) return false;
            if (p.y < rMin.y || p.y > rMax.y) return false;
            if (dims == 3 && (p.z < rMin.z || p.z > rMax.z)) return false;
            return true;
        }

        /// <summary>Min squared distance from a point to an AABB (0 if inside).</summary>
        public static float BoundsMinDistSq(OctreeNode node, Vector3 p, int dims)
        {
            float dx = Math.Max(0f, Math.Max(node.BoundsMin.x - p.x, p.x - node.BoundsMax.x));
            float dy = Math.Max(0f, Math.Max(node.BoundsMin.y - p.y, p.y - node.BoundsMax.y));
            float dz = 0f;
            if (dims == 3)
                dz = Math.Max(0f, Math.Max(node.BoundsMin.z - p.z, p.z - node.BoundsMax.z));
            return dx * dx + dy * dy + dz * dz;
        }

        public static float DistSq(Vector3 a, Vector3 b, int dims)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = dims == 3 ? a.z - b.z : 0f;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
