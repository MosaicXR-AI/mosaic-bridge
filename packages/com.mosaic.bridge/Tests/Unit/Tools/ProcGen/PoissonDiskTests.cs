using System;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.ProcGen;

namespace Mosaic.Bridge.Tests.Unit.Tools.ProcGen
{
    [TestFixture]
    [Category("Unit")]
    public class PoissonDiskTests
    {
        // -----------------------------------------------------------------
        // Helper: call tool with given params
        // -----------------------------------------------------------------
        private static ProcGenPoissonDiskResult Run(ProcGenPoissonDiskParams p, bool expectSuccess = true)
        {
            var result = ProcGenPoissonDiskTool.Execute(p);
            if (expectSuccess)
            {
                Assert.IsTrue(result.Success, $"Expected success but got error: {result.Error}");
                Assert.IsNotNull(result.Data);
                return result.Data;
            }
            Assert.IsFalse(result.Success);
            return null;
        }

        // -----------------------------------------------------------------
        // All points respect minDistance (pairwise validation)
        // -----------------------------------------------------------------
        [Test]
        public void AllPoints_RespectMinDistance_2D()
        {
            float minDist = 2f;
            var data = Run(new ProcGenPoissonDiskParams
            {
                BoundsMax   = new[] { 20f, 0f, 20f },
                MinDistance  = minDist,
                Seed        = 42,
                Dimensions  = 2
            });

            Assert.Greater(data.Count, 0, "Should generate at least one point");
            Assert.AreEqual(data.Count, data.Points.Length);

            float minDistSq = minDist * minDist;
            for (int i = 0; i < data.Points.Length; i++)
            {
                for (int j = i + 1; j < data.Points.Length; j++)
                {
                    float dx = data.Points[i][0] - data.Points[j][0];
                    float dz = data.Points[i][2] - data.Points[j][2];
                    float distSq = dx * dx + dz * dz;
                    Assert.GreaterOrEqual(distSq, minDistSq * 0.999f,
                        $"Points {i} and {j} are too close: dist={Math.Sqrt(distSq):F4}, min={minDist}");
                }
            }
        }

        [Test]
        public void AllPoints_RespectMinDistance_3D()
        {
            float minDist = 3f;
            var data = Run(new ProcGenPoissonDiskParams
            {
                BoundsMax   = new[] { 15f, 15f, 15f },
                MinDistance  = minDist,
                Seed        = 7,
                Dimensions  = 3
            });

            Assert.Greater(data.Count, 0);

            float minDistSq = minDist * minDist;
            for (int i = 0; i < data.Points.Length; i++)
            {
                for (int j = i + 1; j < data.Points.Length; j++)
                {
                    float dx = data.Points[i][0] - data.Points[j][0];
                    float dy = data.Points[i][1] - data.Points[j][1];
                    float dz = data.Points[i][2] - data.Points[j][2];
                    float distSq = dx * dx + dy * dy + dz * dz;
                    Assert.GreaterOrEqual(distSq, minDistSq * 0.999f,
                        $"Points {i} and {j} are too close in 3D");
                }
            }
        }

        // -----------------------------------------------------------------
        // Seed produces deterministic output
        // -----------------------------------------------------------------
        [Test]
        public void SameSeed_ProducesDeterministicOutput()
        {
            var p = new ProcGenPoissonDiskParams
            {
                BoundsMax  = new[] { 20f, 0f, 20f },
                MinDistance = 2f,
                Seed       = 123,
                Dimensions = 2
            };

            var data1 = Run(p);
            var data2 = Run(p);

            Assert.AreEqual(data1.Count, data2.Count, "Point counts differ between identical seeds");
            for (int i = 0; i < data1.Count; i++)
            {
                Assert.AreEqual(data1.Points[i][0], data2.Points[i][0], 0.0001f, $"Point {i} X differs");
                Assert.AreEqual(data1.Points[i][2], data2.Points[i][2], 0.0001f, $"Point {i} Z differs");
            }
        }

        // -----------------------------------------------------------------
        // MaxSamples limits count
        // -----------------------------------------------------------------
        [Test]
        public void MaxSamples_LimitsPointCount()
        {
            int maxSamples = 5;
            var data = Run(new ProcGenPoissonDiskParams
            {
                BoundsMax   = new[] { 100f, 0f, 100f },
                MinDistance  = 1f,
                Seed        = 99,
                MaxSamples  = maxSamples,
                Dimensions  = 2
            });

            Assert.LessOrEqual(data.Count, maxSamples,
                $"Expected at most {maxSamples} points but got {data.Count}");
        }

        // -----------------------------------------------------------------
        // Invalid bounds returns error
        // -----------------------------------------------------------------
        [Test]
        public void InvalidBounds_MaxLessThanMin_ReturnsError()
        {
            var result = ProcGenPoissonDiskTool.Execute(new ProcGenPoissonDiskParams
            {
                BoundsMin  = new[] { 10f, 0f, 10f },
                BoundsMax  = new[] { 5f, 0f, 5f },
                MinDistance = 1f
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void InvalidBounds_NullBoundsMax_ReturnsError()
        {
            var result = ProcGenPoissonDiskTool.Execute(new ProcGenPoissonDiskParams
            {
                BoundsMax  = null,
                MinDistance = 1f
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ZeroMinDistance_ReturnsError()
        {
            var result = ProcGenPoissonDiskTool.Execute(new ProcGenPoissonDiskParams
            {
                BoundsMax  = new[] { 10f, 0f, 10f },
                MinDistance = 0f
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void InvalidDimensions_ReturnsError()
        {
            var result = ProcGenPoissonDiskTool.Execute(new ProcGenPoissonDiskParams
            {
                BoundsMax  = new[] { 10f, 10f, 10f },
                MinDistance = 1f,
                Dimensions = 4
            });

            Assert.IsFalse(result.Success);
        }

        // -----------------------------------------------------------------
        // PrefabPath with non-existent prefab fails gracefully
        // -----------------------------------------------------------------
        [Test]
        public void NonExistentPrefab_ReturnsError()
        {
            var result = ProcGenPoissonDiskTool.Execute(new ProcGenPoissonDiskParams
            {
                BoundsMax   = new[] { 10f, 0f, 10f },
                MinDistance  = 2f,
                PrefabPath  = "Assets/NonExistent/FakePrefab.prefab"
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Prefab not found"));
        }

        // -----------------------------------------------------------------
        // Points are within bounds
        // -----------------------------------------------------------------
        [Test]
        public void AllPoints_AreWithinBounds()
        {
            float[] bMin = { 5f, 0f, 5f };
            float[] bMax = { 25f, 0f, 25f };
            var data = Run(new ProcGenPoissonDiskParams
            {
                BoundsMin  = bMin,
                BoundsMax  = bMax,
                MinDistance = 2f,
                Seed       = 1,
                Dimensions = 2
            });

            foreach (var pt in data.Points)
            {
                Assert.GreaterOrEqual(pt[0], bMin[0], "Point X below BoundsMin");
                Assert.Less(pt[0], bMax[0], "Point X above BoundsMax");
                Assert.GreaterOrEqual(pt[2], bMin[2], "Point Z below BoundsMin");
                Assert.Less(pt[2], bMax[2], "Point Z above BoundsMax");
            }
        }

        // -----------------------------------------------------------------
        // BoundsUsed is returned correctly
        // -----------------------------------------------------------------
        [Test]
        public void BoundsUsed_ReturnedCorrectly()
        {
            float[] bMax = { 10f, 0f, 10f };
            var data = Run(new ProcGenPoissonDiskParams
            {
                BoundsMax  = bMax,
                MinDistance = 2f,
                Seed       = 1
            });

            Assert.AreEqual(0f, data.BoundsUsedMin[0]);
            Assert.AreEqual(0f, data.BoundsUsedMin[1]);
            Assert.AreEqual(0f, data.BoundsUsedMin[2]);
            Assert.AreEqual(bMax[0], data.BoundsUsedMax[0]);
            Assert.AreEqual(bMax[1], data.BoundsUsedMax[1]);
            Assert.AreEqual(bMax[2], data.BoundsUsedMax[2]);
        }

        // -----------------------------------------------------------------
        // No prefab -> GameObjectNames is null
        // -----------------------------------------------------------------
        [Test]
        public void NoPrefab_GameObjectNamesIsNull()
        {
            var data = Run(new ProcGenPoissonDiskParams
            {
                BoundsMax  = new[] { 10f, 0f, 10f },
                MinDistance = 2f,
                Seed       = 1
            });

            Assert.IsNull(data.GameObjectNames);
        }
    }
}
