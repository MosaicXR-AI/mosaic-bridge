using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Spatial;

namespace Mosaic.Bridge.Tests.Unit.Tools.Spatial
{
    [TestFixture]
    [Category("Unit")]
    public class OctreeTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            OctreeStore.Clear();
            _spawned.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            OctreeStore.Clear();
            foreach (var go in _spawned)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        private static SpatialOctreeCreateParams.Point MakePoint(string id, float x, float y, float z, string data = null)
            => new SpatialOctreeCreateParams.Point
            {
                Id       = id,
                Position = new[] { x, y, z },
                Data     = data
            };

        private static List<SpatialOctreeCreateParams.Point> MakeRandom3D(int n, int seed, float range = 10f)
        {
            var rng = new System.Random(seed);
            var list = new List<SpatialOctreeCreateParams.Point>(n);
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rng.NextDouble() * range * 2 - range);
                float y = (float)(rng.NextDouble() * range * 2 - range);
                float z = (float)(rng.NextDouble() * range * 2 - range);
                list.Add(MakePoint($"p{i}", x, y, z));
            }
            return list;
        }

        // -----------------------------------------------------------------
        // 1. 3D octree, range query returns subset
        // -----------------------------------------------------------------
        [Test]
        public void Octree3D_RangeQuery_ReturnsSubset()
        {
            var points = MakeRandom3D(100, seed: 42);
            var create = SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId = "test3d",
                Dimensions  = 3,
                BoundsMin   = new float[] { -10f, -10f, -10f },
                BoundsMax   = new float[] {  10f,  10f,  10f },
                Points      = points
            });
            Assert.IsTrue(create.Success, create.Error);
            Assert.AreEqual(100, create.Data.PointCount);

            var query = SpatialOctreeQueryTool.Execute(new SpatialOctreeQueryParams
            {
                StructureId = "test3d",
                QueryType   = "range",
                Range       = new[]
                {
                    new float[] { 0f, 0f, 0f },
                    new float[] { 10f, 10f, 10f }
                }
            });
            Assert.IsTrue(query.Success, query.Error);

            // Brute-force count of points inside +++ octant
            int expected = 0;
            foreach (var p in points)
                if (p.Position[0] >= 0 && p.Position[1] >= 0 && p.Position[2] >= 0)
                    expected++;

            Assert.AreEqual(expected, query.Data.Count,
                "Range query should return all points inside the AABB");
            Assert.Greater(query.Data.NodesVisited, 0);
            Assert.Less(query.Data.Count, 100, "Should be a proper subset");
        }

        // -----------------------------------------------------------------
        // 2. 2D quadtree works
        // -----------------------------------------------------------------
        [Test]
        public void Quadtree2D_Works()
        {
            var points = new List<SpatialOctreeCreateParams.Point>
            {
                MakePoint("a",  1f,  1f, 0f),
                MakePoint("b", -1f, -1f, 0f),
                MakePoint("c",  2f,  3f, 0f),
                MakePoint("d", -2f,  3f, 0f)
            };
            var create = SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId = "quad",
                Dimensions  = 2,
                BoundsMin   = new float[] { -5f, -5f, 0f },
                BoundsMax   = new float[] {  5f,  5f, 0f },
                Points      = points
            });
            Assert.IsTrue(create.Success, create.Error);
            Assert.AreEqual(2, create.Data.Dimensions);

            // Range query: +x +y quadrant
            var query = SpatialOctreeQueryTool.Execute(new SpatialOctreeQueryParams
            {
                StructureId = "quad",
                QueryType   = "range",
                Range       = new[]
                {
                    new float[] { 0f, 0f, -1f },
                    new float[] { 5f, 5f,  1f }
                }
            });
            Assert.IsTrue(query.Success, query.Error);
            // Expect 'a' and 'c'
            Assert.AreEqual(2, query.Data.Count);
        }

        // -----------------------------------------------------------------
        // 3. Nearest neighbor to arbitrary point
        // -----------------------------------------------------------------
        [Test]
        public void NearestNeighbor_ReturnsClosest()
        {
            var points = new List<SpatialOctreeCreateParams.Point>
            {
                MakePoint("far",  9f, 9f, 9f),
                MakePoint("mid",  5f, 5f, 5f),
                MakePoint("near", 1f, 1f, 1f)
            };
            var create = SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId = "nn",
                Dimensions  = 3,
                BoundsMin   = new float[] { -10f, -10f, -10f },
                BoundsMax   = new float[] {  10f,  10f,  10f },
                Points      = points
            });
            Assert.IsTrue(create.Success, create.Error);

            var query = SpatialOctreeQueryTool.Execute(new SpatialOctreeQueryParams
            {
                StructureId = "nn",
                QueryType   = "nearest",
                Position    = new float[] { 0f, 0f, 0f }
            });
            Assert.IsTrue(query.Success, query.Error);
            Assert.AreEqual(1, query.Data.Count);
            Assert.AreEqual("near", query.Data.Points[0].Id);
            Assert.AreEqual(Mathf.Sqrt(3f), query.Data.Points[0].Distance, 1e-4f);
        }

        // -----------------------------------------------------------------
        // 4. KNN returns K closest, sorted
        // -----------------------------------------------------------------
        [Test]
        public void Knn_ReturnsKClosestSorted()
        {
            var points = MakeRandom3D(50, seed: 7);
            var create = SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId = "knn",
                Dimensions  = 3,
                BoundsMin   = new float[] { -10f, -10f, -10f },
                BoundsMax   = new float[] {  10f,  10f,  10f },
                Points      = points
            });
            Assert.IsTrue(create.Success, create.Error);

            int k = 5;
            var query = SpatialOctreeQueryTool.Execute(new SpatialOctreeQueryParams
            {
                StructureId = "knn",
                QueryType   = "knn",
                Position    = new float[] { 0f, 0f, 0f },
                K           = k
            });
            Assert.IsTrue(query.Success, query.Error);
            Assert.AreEqual(k, query.Data.Count);

            // Distances should be non-decreasing
            for (int i = 1; i < query.Data.Points.Count; i++)
                Assert.LessOrEqual(query.Data.Points[i - 1].Distance,
                                   query.Data.Points[i].Distance);

            // Brute-force: best K distances must match
            var brute = new List<float>();
            foreach (var p in points)
            {
                float dx = p.Position[0], dy = p.Position[1], dz = p.Position[2];
                brute.Add(Mathf.Sqrt(dx * dx + dy * dy + dz * dz));
            }
            brute.Sort();
            for (int i = 0; i < k; i++)
                Assert.AreEqual(brute[i], query.Data.Points[i].Distance, 1e-4f);
        }

        // -----------------------------------------------------------------
        // 5. Radius query returns points within radius
        // -----------------------------------------------------------------
        [Test]
        public void RadiusQuery_ReturnsPointsWithinRadius()
        {
            var points = new List<SpatialOctreeCreateParams.Point>
            {
                MakePoint("in1",  1f, 0f, 0f),
                MakePoint("in2",  0f, 2f, 0f),
                MakePoint("out1", 5f, 0f, 0f),
                MakePoint("out2", 3f, 3f, 3f)
            };
            var create = SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId = "rad",
                Dimensions  = 3,
                BoundsMin   = new float[] { -10f, -10f, -10f },
                BoundsMax   = new float[] {  10f,  10f,  10f },
                Points      = points
            });
            Assert.IsTrue(create.Success, create.Error);

            var query = SpatialOctreeQueryTool.Execute(new SpatialOctreeQueryParams
            {
                StructureId = "rad",
                QueryType   = "radius",
                Position    = new float[] { 0f, 0f, 0f },
                Radius      = 2.5f
            });
            Assert.IsTrue(query.Success, query.Error);
            Assert.AreEqual(2, query.Data.Count);

            var ids = new HashSet<string>();
            foreach (var pt in query.Data.Points) ids.Add(pt.Id);
            Assert.IsTrue(ids.Contains("in1"));
            Assert.IsTrue(ids.Contains("in2"));
        }

        // -----------------------------------------------------------------
        // 6. Subdivision triggered at MaxPointsPerNode
        // -----------------------------------------------------------------
        [Test]
        public void Subdivision_TriggeredAtMaxPointsPerNode()
        {
            // 9 points with MaxPointsPerNode=2 must force subdivision
            var points = new List<SpatialOctreeCreateParams.Point>();
            for (int i = 0; i < 9; i++)
                points.Add(MakePoint($"p{i}", i - 4f, 0f, 0f));

            var create = SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId      = "sub",
                Dimensions       = 3,
                BoundsMin        = new float[] { -10f, -10f, -10f },
                BoundsMax        = new float[] {  10f,  10f,  10f },
                MaxDepth         = 4,
                MaxPointsPerNode = 2,
                Points           = points
            });
            Assert.IsTrue(create.Success, create.Error);
            Assert.Greater(create.Data.NodeCount, 1,
                "Octree should have subdivided beyond the root node");
            Assert.Greater(create.Data.MaxDepthReached, 0,
                "MaxDepthReached should be > 0 after subdivision");
        }

        // -----------------------------------------------------------------
        // 7. Invalid bounds returns error
        // -----------------------------------------------------------------
        [Test]
        public void InvalidBounds_ReturnsError()
        {
            var result = SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId = "bad",
                Dimensions  = 3,
                BoundsMin   = new float[] { 5f, 5f, 5f },
                BoundsMax   = new float[] { 0f, 0f, 0f }
            });
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("BoundsMax"));
        }

        [Test]
        public void InvalidDimensions_ReturnsError()
        {
            var result = SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId = "bad",
                Dimensions  = 5,
                BoundsMin   = new float[] { 0f, 0f, 0f },
                BoundsMax   = new float[] { 1f, 1f, 1f }
            });
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Dimensions"));
        }

        // -----------------------------------------------------------------
        // 8. Query non-existent structure returns error
        // -----------------------------------------------------------------
        [Test]
        public void QueryNonExistentStructure_ReturnsError()
        {
            var result = SpatialOctreeQueryTool.Execute(new SpatialOctreeQueryParams
            {
                StructureId = "does-not-exist",
                QueryType   = "nearest",
                Position    = new float[] { 0f, 0f, 0f }
            });
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("not found"));
        }

        // -----------------------------------------------------------------
        // 9. Populate from GameObjects
        // -----------------------------------------------------------------
        [Test]
        public void FromGameObjects_PopulatesFromScene()
        {
            var goA = new GameObject("OctreeTest_GO_A");
            goA.transform.position = new Vector3(1f, 2f, 3f);
            var goB = new GameObject("OctreeTest_GO_B");
            goB.transform.position = new Vector3(-1f, -2f, -3f);
            _spawned.Add(goA);
            _spawned.Add(goB);

            var create = SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId = "go",
                Dimensions  = 3,
                BoundsMin   = new float[] { -10f, -10f, -10f },
                BoundsMax   = new float[] {  10f,  10f,  10f },
                GameObjects = new[] { "OctreeTest_GO_A", "OctreeTest_GO_B" }
            });
            Assert.IsTrue(create.Success, create.Error);
            Assert.AreEqual(2, create.Data.PointCount);

            var query = SpatialOctreeQueryTool.Execute(new SpatialOctreeQueryParams
            {
                StructureId = "go",
                QueryType   = "nearest",
                Position    = new float[] { 1f, 2f, 3f }
            });
            Assert.IsTrue(query.Success, query.Error);
            Assert.AreEqual("OctreeTest_GO_A", query.Data.Points[0].Id);
            Assert.AreEqual(0f, query.Data.Points[0].Distance, 1e-4f);
        }

        [Test]
        public void FromGameObjects_NotFound_ReturnsError()
        {
            var result = SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId = "go-missing",
                Dimensions  = 3,
                BoundsMin   = new float[] { -10f, -10f, -10f },
                BoundsMax   = new float[] {  10f,  10f,  10f },
                GameObjects = new[] { "NonExistent_DoesNotExist_12345" }
            });
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("not found"));
        }

        // -----------------------------------------------------------------
        // Extra: bad query type returns error
        // -----------------------------------------------------------------
        [Test]
        public void InvalidQueryType_ReturnsError()
        {
            SpatialOctreeCreateTool.Execute(new SpatialOctreeCreateParams
            {
                StructureId = "qt",
                Dimensions  = 3,
                BoundsMin   = new float[] { -1f, -1f, -1f },
                BoundsMax   = new float[] {  1f,  1f,  1f }
            });

            var result = SpatialOctreeQueryTool.Execute(new SpatialOctreeQueryParams
            {
                StructureId = "qt",
                QueryType   = "bogus"
            });
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Unknown QueryType"));
        }
    }
}
