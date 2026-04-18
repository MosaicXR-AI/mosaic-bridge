using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mosaic.Bridge.Tools.Spatial;

namespace Mosaic.Bridge.Tests.Unit.Tools.Spatial
{
    [TestFixture]
    [Category("Unit")]
    public class KdtreeTests
    {
        // ─── Helpers ────────────────────────────────────────────────────
        private static List<SpatialKdtreeCreateParams.Point> RandomPoints3D(int count, int seed)
        {
            var rng = new Random(seed);
            var list = new List<SpatialKdtreeCreateParams.Point>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new SpatialKdtreeCreateParams.Point
                {
                    Id       = $"p{i}",
                    Position = new[]
                    {
                        (float)(rng.NextDouble() * 100.0),
                        (float)(rng.NextDouble() * 100.0),
                        (float)(rng.NextDouble() * 100.0)
                    },
                    Data = null
                });
            }
            return list;
        }

        private static float Dist(float[] a, float[] b)
        {
            float s = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                float d = a[i] - b[i];
                s += d * d;
            }
            return (float)Math.Sqrt(s);
        }

        private static SpatialKdtreeCreateResult BuildOrFail(SpatialKdtreeCreateParams p)
        {
            var res = SpatialKdtreeCreateTool.Execute(p);
            Assert.IsTrue(res.Success, $"Build failed: {res.Error}");
            Assert.IsNotNull(res.Data);
            return res.Data;
        }

        // ─── 1. Build with 100 random 3D points ─────────────────────────
        [Test]
        public void Build_100RandomPoints_Succeeds()
        {
            var pts = RandomPoints3D(100, seed: 1);
            var data = BuildOrFail(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_build_100",
                Dimensions  = 3,
                Points      = pts
            });

            Assert.AreEqual(100, data.PointCount);
            Assert.AreEqual(3, data.Dimensions);
            Assert.Greater(data.TreeDepth, 0);
            // log2(100) ≈ 6.6; balanced tree ≤ ceil(log2)+1 but leave headroom for duplicates
            Assert.LessOrEqual(data.TreeDepth, 20);
        }

        // ─── 2. Nearest neighbor returns correct point ──────────────────
        [Test]
        public void Nearest_ReturnsCorrectPoint()
        {
            var pts = RandomPoints3D(100, seed: 42);
            BuildOrFail(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_nearest",
                Dimensions  = 3,
                Points      = pts
            });

            var query = new[] { 50f, 50f, 50f };

            // Brute force truth
            float bestD = float.PositiveInfinity;
            string bestId = null;
            foreach (var pt in pts)
            {
                float d = Dist(pt.Position, query);
                if (d < bestD) { bestD = d; bestId = pt.Id; }
            }

            var res = SpatialKdtreeQueryTool.Execute(new SpatialKdtreeQueryParams
            {
                StructureId = "kd_nearest",
                QueryType   = "nearest",
                Position    = query
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual(1, res.Data.Count);
            Assert.AreEqual(bestId, res.Data.Points[0].Id);
            Assert.AreEqual(bestD, res.Data.Points[0].Distance, 1e-3f);
        }

        // ─── 3. KNN returns K closest sorted by distance ────────────────
        [Test]
        public void Knn_ReturnsKClosestSortedByDistance()
        {
            var pts = RandomPoints3D(100, seed: 7);
            BuildOrFail(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_knn",
                Dimensions  = 3,
                Points      = pts
            });

            var query = new[] { 25f, 75f, 50f };
            int k = 5;

            // Brute-force top K
            var sorted = pts
                .Select(p => (id: p.Id, d: Dist(p.Position, query)))
                .OrderBy(t => t.d)
                .Take(k)
                .ToList();

            var res = SpatialKdtreeQueryTool.Execute(new SpatialKdtreeQueryParams
            {
                StructureId = "kd_knn",
                QueryType   = "knn",
                Position    = query,
                K           = k
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual(k, res.Data.Count);

            // Sorted ascending
            for (int i = 1; i < res.Data.Points.Length; i++)
                Assert.LessOrEqual(res.Data.Points[i - 1].Distance, res.Data.Points[i].Distance,
                    "KNN results must be sorted ascending by distance");

            // Same IDs (set match, since ties may permute)
            var expectedIds = sorted.Select(t => t.id).OrderBy(s => s).ToArray();
            var actualIds   = res.Data.Points.Select(p => p.Id).OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(expectedIds, actualIds);
        }

        // ─── 4. Radius query ────────────────────────────────────────────
        [Test]
        public void Radius_ReturnsAllPointsWithinRadius()
        {
            var pts = RandomPoints3D(100, seed: 99);
            BuildOrFail(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_radius",
                Dimensions  = 3,
                Points      = pts
            });

            var query = new[] { 50f, 50f, 50f };
            float radius = 30f;

            var expected = pts
                .Where(p => Dist(p.Position, query) <= radius)
                .Select(p => p.Id)
                .OrderBy(s => s)
                .ToArray();

            var res = SpatialKdtreeQueryTool.Execute(new SpatialKdtreeQueryParams
            {
                StructureId = "kd_radius",
                QueryType   = "radius",
                Position    = query,
                Radius      = radius
            });

            Assert.IsTrue(res.Success, res.Error);
            var actual = res.Data.Points.Select(p => p.Id).OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(expected, actual);

            // All reported distances must be <= radius and ascending
            for (int i = 0; i < res.Data.Points.Length; i++)
                Assert.LessOrEqual(res.Data.Points[i].Distance, radius + 1e-4f);
            for (int i = 1; i < res.Data.Points.Length; i++)
                Assert.LessOrEqual(res.Data.Points[i - 1].Distance, res.Data.Points[i].Distance);
        }

        // ─── 5. Range query in 2D (dim=2) ───────────────────────────────
        [Test]
        public void Range_2D_ReturnsPointsInsideBox()
        {
            var pts = new List<SpatialKdtreeCreateParams.Point>
            {
                new SpatialKdtreeCreateParams.Point { Id = "a", Position = new[] { 1f,  1f } },
                new SpatialKdtreeCreateParams.Point { Id = "b", Position = new[] { 5f,  5f } },
                new SpatialKdtreeCreateParams.Point { Id = "c", Position = new[] { 9f,  9f } },
                new SpatialKdtreeCreateParams.Point { Id = "d", Position = new[] { 4f,  6f } },
                new SpatialKdtreeCreateParams.Point { Id = "e", Position = new[] { 0f,  0f } },
                new SpatialKdtreeCreateParams.Point { Id = "f", Position = new[] { 10f, 10f } }
            };

            BuildOrFail(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_range2d",
                Dimensions  = 2,
                Points      = pts
            });

            var res = SpatialKdtreeQueryTool.Execute(new SpatialKdtreeQueryParams
            {
                StructureId = "kd_range2d",
                QueryType   = "range",
                RangeMin    = new[] { 2f, 2f },
                RangeMax    = new[] { 8f, 8f }
            });

            Assert.IsTrue(res.Success, res.Error);
            var ids = res.Data.Points.Select(p => p.Id).OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(new[] { "b", "d" }, ids);
        }

        // ─── 6. Points outside range excluded ───────────────────────────
        [Test]
        public void Range_ExcludesPointsOutsideBox()
        {
            var pts = new List<SpatialKdtreeCreateParams.Point>
            {
                new SpatialKdtreeCreateParams.Point { Id = "inside",  Position = new[] { 5f, 5f, 5f } },
                new SpatialKdtreeCreateParams.Point { Id = "outside", Position = new[] { 100f, 100f, 100f } }
            };

            BuildOrFail(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_range_excl",
                Dimensions  = 3,
                Points      = pts
            });

            var res = SpatialKdtreeQueryTool.Execute(new SpatialKdtreeQueryParams
            {
                StructureId = "kd_range_excl",
                QueryType   = "range",
                RangeMin    = new[] { 0f, 0f, 0f },
                RangeMax    = new[] { 10f, 10f, 10f }
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual(1, res.Data.Count);
            Assert.AreEqual("inside", res.Data.Points[0].Id);
        }

        // ─── 7. Deterministic given same input order ────────────────────
        [Test]
        public void Build_IsDeterministic_ForSameInputOrder()
        {
            var pts1 = RandomPoints3D(50, seed: 123);
            var pts2 = RandomPoints3D(50, seed: 123);

            var a = BuildOrFail(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_det_a", Dimensions = 3, Points = pts1
            });
            var b = BuildOrFail(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_det_b", Dimensions = 3, Points = pts2
            });

            Assert.AreEqual(a.PointCount, b.PointCount);
            Assert.AreEqual(a.TreeDepth,  b.TreeDepth);

            var q = new[] { 50f, 50f, 50f };
            var ra = SpatialKdtreeQueryTool.Execute(new SpatialKdtreeQueryParams
            {
                StructureId = "kd_det_a", QueryType = "knn", Position = q, K = 5
            }).Data;
            var rb = SpatialKdtreeQueryTool.Execute(new SpatialKdtreeQueryParams
            {
                StructureId = "kd_det_b", QueryType = "knn", Position = q, K = 5
            }).Data;

            CollectionAssert.AreEqual(
                ra.Points.Select(p => p.Id).ToArray(),
                rb.Points.Select(p => p.Id).ToArray());
        }

        // ─── 8. Invalid dimensions returns error ────────────────────────
        [Test]
        public void InvalidDimensions_ReturnsError()
        {
            var resLow = SpatialKdtreeCreateTool.Execute(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_bad_dim_low",
                Dimensions  = 0,
                Points      = new List<SpatialKdtreeCreateParams.Point>
                {
                    new SpatialKdtreeCreateParams.Point { Id = "x", Position = new[] { 1f } }
                }
            });
            Assert.IsFalse(resLow.Success);
            Assert.IsTrue(resLow.Error.Contains("Dimensions"));

            var resHigh = SpatialKdtreeCreateTool.Execute(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_bad_dim_high",
                Dimensions  = 5,
                Points      = new List<SpatialKdtreeCreateParams.Point>
                {
                    new SpatialKdtreeCreateParams.Point { Id = "x", Position = new[] { 1f, 2f, 3f, 4f, 5f } }
                }
            });
            Assert.IsFalse(resHigh.Success);
            Assert.IsTrue(resHigh.Error.Contains("Dimensions"));
        }

        // ─── Edge: unknown query type ───────────────────────────────────
        [Test]
        public void Query_UnknownType_ReturnsError()
        {
            BuildOrFail(new SpatialKdtreeCreateParams
            {
                StructureId = "kd_unk",
                Dimensions  = 3,
                Points      = RandomPoints3D(10, seed: 1)
            });

            var res = SpatialKdtreeQueryTool.Execute(new SpatialKdtreeQueryParams
            {
                StructureId = "kd_unk",
                QueryType   = "bogus",
                Position    = new[] { 1f, 2f, 3f }
            });
            Assert.IsFalse(res.Success);
            Assert.IsTrue(res.Error.Contains("Unknown QueryType"));
        }

        // ─── Edge: missing structure ────────────────────────────────────
        [Test]
        public void Query_MissingStructure_ReturnsNotFound()
        {
            var res = SpatialKdtreeQueryTool.Execute(new SpatialKdtreeQueryParams
            {
                StructureId = "kd_does_not_exist_xyz",
                QueryType   = "nearest",
                Position    = new[] { 0f, 0f, 0f }
            });
            Assert.IsFalse(res.Success);
            Assert.IsTrue(res.Error.Contains("No KD-tree"));
        }
    }
}
