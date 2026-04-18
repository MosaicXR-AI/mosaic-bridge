using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Spatial;

namespace Mosaic.Bridge.Tests.Unit.Tools.Spatial
{
    [TestFixture]
    [Category("Unit")]
    public class SpatialHashTests
    {
        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        private static SpatialHashCreateResult CreateGrid(
            string id, float cellSize, int dims, List<SpatialHashPoint> points,
            bool expectSuccess = true)
        {
            var res = SpatialHashCreateTool.Execute(new SpatialHashCreateParams
            {
                StructureId = id,
                CellSize    = cellSize,
                Dimensions  = dims,
                Points      = points
            });
            if (expectSuccess)
            {
                Assert.IsTrue(res.Success, $"Expected success but got error: {res.Error}");
                Assert.IsNotNull(res.Data);
                return res.Data;
            }
            Assert.IsFalse(res.Success);
            return null;
        }

        private static List<SpatialHashPoint> MakeGridPoints(int countPerAxis, float spacing)
        {
            var list = new List<SpatialHashPoint>();
            int i = 0;
            for (int x = 0; x < countPerAxis; x++)
            for (int y = 0; y < countPerAxis; y++)
            for (int z = 0; z < countPerAxis; z++)
            {
                list.Add(new SpatialHashPoint
                {
                    Id       = $"p_{i}",
                    Position = new[] { x * spacing, y * spacing, z * spacing }
                });
                i++;
            }
            return list;
        }

        // -----------------------------------------------------------------
        // Create with 100 points → CellCount > 0
        // -----------------------------------------------------------------
        [Test]
        public void Create_WithHundredPoints_HasPositiveCellCount()
        {
            // 5x5x4 = 100 points
            var points = new List<SpatialHashPoint>();
            int i = 0;
            for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
            for (int z = 0; z < 4; z++)
            {
                points.Add(new SpatialHashPoint
                {
                    Id       = $"p_{i++}",
                    Position = new[] { x * 1f, y * 1f, z * 1f }
                });
            }

            var data = CreateGrid("hundred", 2f, 3, points);
            Assert.AreEqual(100, data.PointCount);
            Assert.Greater(data.CellCount, 0);
            Assert.GreaterOrEqual(data.MaxPointsInCell, 1);
        }

        // -----------------------------------------------------------------
        // Radius query returns points within radius
        // -----------------------------------------------------------------
        [Test]
        public void Radius_ReturnsPointsWithinRadius()
        {
            var points = MakeGridPoints(5, 1f); // 125 points spacing 1
            CreateGrid("radius_grid", 2f, 3, points);

            var res = SpatialHashQueryTool.Execute(new SpatialHashQueryParams
            {
                StructureId = "radius_grid",
                QueryType   = "radius",
                Position    = new[] { 2f, 2f, 2f },
                Radius      = 1.5f
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.Greater(res.Data.Count, 0);

            // Verify every returned point is actually within radius
            foreach (var p in res.Data.Points)
            {
                float dx = p.Position[0] - 2f;
                float dy = p.Position[1] - 2f;
                float dz = p.Position[2] - 2f;
                float d  = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                Assert.LessOrEqual(d, 1.5001f,
                    $"Point {p.Id} at ({p.Position[0]},{p.Position[1]},{p.Position[2]}) dist={d} exceeds radius");
                Assert.AreEqual(d, p.Distance, 0.0001f);
            }

            // Verify completeness: count points within radius by brute force
            int expected = 0;
            foreach (var pt in points)
            {
                float dx = pt.Position[0] - 2f;
                float dy = pt.Position[1] - 2f;
                float dz = pt.Position[2] - 2f;
                if (Mathf.Sqrt(dx * dx + dy * dy + dz * dz) <= 1.5f) expected++;
            }
            Assert.AreEqual(expected, res.Data.Count);
        }

        // -----------------------------------------------------------------
        // Cell query returns points in specific cell
        // -----------------------------------------------------------------
        [Test]
        public void Cell_ReturnsPointsInSpecificCell()
        {
            var points = new List<SpatialHashPoint>
            {
                // cell (0,0,0) with cellSize 2 -> positions in [0,2)
                new SpatialHashPoint { Id = "a", Position = new[] { 0.1f, 0.2f, 0.3f } },
                new SpatialHashPoint { Id = "b", Position = new[] { 1.9f, 0.5f, 1.0f } },
                // cell (1,0,0)
                new SpatialHashPoint { Id = "c", Position = new[] { 2.1f, 0.5f, 1.0f } }
            };

            CreateGrid("cell_grid", 2f, 3, points);

            var res = SpatialHashQueryTool.Execute(new SpatialHashQueryParams
            {
                StructureId = "cell_grid",
                QueryType   = "cell",
                CellCoord   = new[] { 0, 0, 0 }
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual(2, res.Data.Count);
            Assert.AreEqual(1, res.Data.CellsVisited);

            var ids = new HashSet<string>();
            foreach (var p in res.Data.Points) ids.Add(p.Id);
            Assert.IsTrue(ids.Contains("a"));
            Assert.IsTrue(ids.Contains("b"));
            Assert.IsFalse(ids.Contains("c"));
        }

        // -----------------------------------------------------------------
        // Range query returns points in box
        // -----------------------------------------------------------------
        [Test]
        public void Range_ReturnsPointsInBox()
        {
            var points = MakeGridPoints(5, 1f); // 0..4 on each axis
            CreateGrid("range_grid", 2f, 3, points);

            var res = SpatialHashQueryTool.Execute(new SpatialHashQueryParams
            {
                StructureId = "range_grid",
                QueryType   = "range",
                RangeMin    = new[] { 1f, 1f, 1f },
                RangeMax    = new[] { 3f, 3f, 3f }
            });

            Assert.IsTrue(res.Success, res.Error);
            // points with each axis in {1,2,3} => 3^3 = 27
            Assert.AreEqual(27, res.Data.Count);

            foreach (var p in res.Data.Points)
            {
                Assert.GreaterOrEqual(p.Position[0], 1f);
                Assert.LessOrEqual(p.Position[0], 3f);
                Assert.GreaterOrEqual(p.Position[1], 1f);
                Assert.LessOrEqual(p.Position[1], 3f);
                Assert.GreaterOrEqual(p.Position[2], 1f);
                Assert.LessOrEqual(p.Position[2], 3f);
            }
        }

        // -----------------------------------------------------------------
        // Invalid CellSize (0, negative) returns error
        // -----------------------------------------------------------------
        [Test]
        public void InvalidCellSize_ReturnsError()
        {
            var r0 = SpatialHashCreateTool.Execute(new SpatialHashCreateParams
            {
                StructureId = "bad0",
                CellSize    = 0f,
                Dimensions  = 3
            });
            Assert.IsFalse(r0.Success);

            var rNeg = SpatialHashCreateTool.Execute(new SpatialHashCreateParams
            {
                StructureId = "badNeg",
                CellSize    = -1f,
                Dimensions  = 3
            });
            Assert.IsFalse(rNeg.Success);
        }

        // -----------------------------------------------------------------
        // Query non-existent structure returns error
        // -----------------------------------------------------------------
        [Test]
        public void Query_NonExistentStructure_ReturnsError()
        {
            var res = SpatialHashQueryTool.Execute(new SpatialHashQueryParams
            {
                StructureId = "does_not_exist_xyz",
                QueryType   = "radius",
                Position    = new[] { 0f, 0f, 0f },
                Radius      = 1f
            });
            Assert.IsFalse(res.Success);
        }

        // -----------------------------------------------------------------
        // MaxPointsInCell reflects the densest cell correctly
        // -----------------------------------------------------------------
        [Test]
        public void MaxPointsInCell_ReflectsDensestCell()
        {
            // Put 5 points in one cell, 1 in another.
            var points = new List<SpatialHashPoint>
            {
                new SpatialHashPoint { Id = "a", Position = new[] { 0.1f, 0.1f, 0.1f } },
                new SpatialHashPoint { Id = "b", Position = new[] { 0.2f, 0.2f, 0.2f } },
                new SpatialHashPoint { Id = "c", Position = new[] { 0.3f, 0.3f, 0.3f } },
                new SpatialHashPoint { Id = "d", Position = new[] { 0.4f, 0.4f, 0.4f } },
                new SpatialHashPoint { Id = "e", Position = new[] { 0.5f, 0.5f, 0.5f } },
                new SpatialHashPoint { Id = "f", Position = new[] { 10f, 10f, 10f } }
            };

            var data = CreateGrid("density_grid", 1f, 3, points);
            Assert.AreEqual(6, data.PointCount);
            Assert.AreEqual(2, data.CellCount);
            Assert.AreEqual(5, data.MaxPointsInCell);
        }

        // -----------------------------------------------------------------
        // 2D mode: Z term is dropped, points on same XY fall in same cell
        // -----------------------------------------------------------------
        [Test]
        public void TwoDimensional_IgnoresZ()
        {
            var points = new List<SpatialHashPoint>
            {
                new SpatialHashPoint { Id = "a", Position = new[] { 0.5f, 0.5f, 0f } },
                new SpatialHashPoint { Id = "b", Position = new[] { 0.5f, 0.5f, 100f } }
            };

            var data = CreateGrid("grid2d", 1f, 2, points);
            Assert.AreEqual(1, data.CellCount);
            Assert.AreEqual(2, data.MaxPointsInCell);
        }

        // -----------------------------------------------------------------
        // Missing QueryType returns error
        // -----------------------------------------------------------------
        [Test]
        public void UnknownQueryType_ReturnsError()
        {
            CreateGrid("bad_qt_grid", 1f, 3, new List<SpatialHashPoint>
            {
                new SpatialHashPoint { Id = "x", Position = new[] { 0f, 0f, 0f } }
            });

            var res = SpatialHashQueryTool.Execute(new SpatialHashQueryParams
            {
                StructureId = "bad_qt_grid",
                QueryType   = "nonsense"
            });
            Assert.IsFalse(res.Success);
        }
    }
}
