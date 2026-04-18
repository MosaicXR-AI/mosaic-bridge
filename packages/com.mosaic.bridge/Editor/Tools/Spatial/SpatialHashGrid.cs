using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mosaic.Bridge.Tools.Spatial
{
    /// <summary>
    /// Internal spatial hash grid structure shared by create / query tools.
    /// </summary>
    internal sealed class SpatialHashGrid
    {
        public string                                 StructureId;
        public float                                  CellSize;
        public int                                    Dimensions;
        public Dictionary<long, List<SpatialHashPoint>> Cells = new Dictionary<long, List<SpatialHashPoint>>();
        public int                                    PointCount;

        public static readonly Dictionary<string, SpatialHashGrid> Registry =
            new Dictionary<string, SpatialHashGrid>();

        public long HashCell(int cx, int cy, int cz)
        {
            // 3D: mix three coords; 2D: drop Z term.
            long h = (long)(cx + 73856093) * 19349663L;
            h ^= (long)cy * 83492791L;
            if (Dimensions == 3)
                h ^= (long)cz;
            return h;
        }

        public int CellX(float x) => Mathf.FloorToInt(x / CellSize);
        public int CellY(float y) => Mathf.FloorToInt(y / CellSize);
        public int CellZ(float z) => Dimensions == 3 ? Mathf.FloorToInt(z / CellSize) : 0;

        public void Insert(SpatialHashPoint p)
        {
            int cx = CellX(p.Position[0]);
            int cy = CellY(p.Position[1]);
            int cz = Dimensions == 3 ? CellZ(p.Position[2]) : 0;
            long key = HashCell(cx, cy, cz);
            if (!Cells.TryGetValue(key, out var bucket))
            {
                bucket = new List<SpatialHashPoint>();
                Cells[key] = bucket;
            }
            bucket.Add(p);
            PointCount++;
        }

        public int MaxPointsInCell()
        {
            int max = 0;
            foreach (var b in Cells.Values)
                if (b.Count > max) max = b.Count;
            return max;
        }
    }
}
