using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.ProcGen;

namespace Mosaic.Bridge.Tests.Unit.Tools.ProcGen
{
    [TestFixture]
    [Category("Unit")]
    public class VoronoiDelaunayTests
    {
        // =================================================================
        // Voronoi Tests
        // =================================================================

        [Test]
        public void Voronoi_ProducesCorrectCellCount_WithPointCount()
        {
            var result = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
            {
                PointCount = 10,
                BoundsMin  = new[] { 0f, 0f },
                BoundsMax  = new[] { 100f, 100f },
                Seed       = 42
            });

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.AreEqual(10, result.Data.CellCount);
            Assert.AreEqual(10, result.Data.Cells.Length);
        }

        [Test]
        public void Voronoi_ProducesCorrectCellCount_WithExplicitPoints()
        {
            var pts = new[]
            {
                new[] { 10f, 10f },
                new[] { 50f, 50f },
                new[] { 90f, 10f },
                new[] { 50f, 90f },
                new[] { 10f, 90f }
            };

            var result = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
            {
                Points    = pts,
                BoundsMin = new[] { 0f, 0f },
                BoundsMax = new[] { 100f, 100f }
            });

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.AreEqual(5, result.Data.CellCount);
            Assert.AreEqual(5, result.Data.Cells.Length);
        }

        [Test]
        public void Voronoi_LloydRelaxation_ProducesMoreEvenCells()
        {
            int pointCount = 20;
            int seed = 42;

            // Without relaxation
            var resultNoRelax = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
            {
                PointCount      = pointCount,
                BoundsMin       = new[] { 0f, 0f },
                BoundsMax       = new[] { 100f, 100f },
                Seed            = seed,
                RelaxIterations = 0
            });

            // With relaxation
            var resultRelaxed = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
            {
                PointCount      = pointCount,
                BoundsMin       = new[] { 0f, 0f },
                BoundsMax       = new[] { 100f, 100f },
                Seed            = seed,
                RelaxIterations = 5
            });

            Assert.IsTrue(resultNoRelax.Success);
            Assert.IsTrue(resultRelaxed.Success);

            // Relaxed cells should have more uniform neighbor counts
            // (lower variance in neighbor counts)
            float varNoRelax = ComputeNeighborVariance(resultNoRelax.Data.Cells);
            float varRelaxed = ComputeNeighborVariance(resultRelaxed.Data.Cells);
            Assert.LessOrEqual(varRelaxed, varNoRelax + 1f,
                "Relaxed diagram should have similar or lower neighbor variance");
        }

        [Test]
        public void Voronoi_Deterministic_WithSeed()
        {
            var p = new ProcGenVoronoiParams
            {
                PointCount = 15,
                BoundsMin  = new[] { 0f, 0f },
                BoundsMax  = new[] { 50f, 50f },
                Seed       = 123
            };

            var result1 = ProcGenVoronoiTool.Execute(p);
            var result2 = ProcGenVoronoiTool.Execute(p);

            Assert.IsTrue(result1.Success);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result1.Data.CellCount, result2.Data.CellCount);

            for (int i = 0; i < result1.Data.Cells.Length; i++)
            {
                Assert.AreEqual(result1.Data.Cells[i].Center[0],
                    result2.Data.Cells[i].Center[0], 0.0001f, $"Cell {i} X differs");
                Assert.AreEqual(result1.Data.Cells[i].Center[1],
                    result2.Data.Cells[i].Center[1], 0.0001f, $"Cell {i} Y differs");
            }
        }

        [Test]
        public void Voronoi_TextureOutput_CreatesFile()
        {
            string savePath = "Assets/Generated/ProcGen/VoronoiTest_Tex";
            string expectedFile = savePath + "/VoronoiDiagram.png";
            string fullPath = Path.Combine(Application.dataPath, "..", expectedFile);

            try
            {
                var result = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
                {
                    PointCount        = 8,
                    BoundsMin         = new[] { 0f, 0f },
                    BoundsMax         = new[] { 50f, 50f },
                    Seed              = 42,
                    Output            = "texture",
                    TextureResolution = 64,
                    SavePath          = savePath
                });

                Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
                Assert.IsNotNull(result.Data.TexturePath);
                Assert.IsTrue(File.Exists(fullPath), $"Texture file not found at {fullPath}");
            }
            finally
            {
                // Cleanup
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
                string metaPath = fullPath + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
                string dir = Path.GetDirectoryName(fullPath);
                if (Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0)
                    Directory.Delete(dir, true);
            }
        }

        [Test]
        public void Voronoi_InvalidBounds_ReturnsError()
        {
            // Null BoundsMin
            var result = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
            {
                PointCount = 10,
                BoundsMin  = null,
                BoundsMax  = new[] { 100f, 100f }
            });
            Assert.IsFalse(result.Success);

            // Null BoundsMax
            result = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
            {
                PointCount = 10,
                BoundsMin  = new[] { 0f, 0f },
                BoundsMax  = null
            });
            Assert.IsFalse(result.Success);

            // Max <= Min
            result = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
            {
                PointCount = 10,
                BoundsMin  = new[] { 50f, 50f },
                BoundsMax  = new[] { 10f, 10f }
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Voronoi_TooFewPoints_ReturnsError()
        {
            var result = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
            {
                PointCount = 1,
                BoundsMin  = new[] { 0f, 0f },
                BoundsMax  = new[] { 100f, 100f }
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Voronoi_InvalidSavePath_ReturnsError()
        {
            var result = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
            {
                PointCount = 5,
                BoundsMin  = new[] { 0f, 0f },
                BoundsMax  = new[] { 50f, 50f },
                Output     = "texture",
                SavePath   = "NotAssets/Bad"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Voronoi_CellsHaveValidCenters()
        {
            var result = ProcGenVoronoiTool.Execute(new ProcGenVoronoiParams
            {
                PointCount = 10,
                BoundsMin  = new[] { 0f, 0f },
                BoundsMax  = new[] { 100f, 100f },
                Seed       = 42
            });

            Assert.IsTrue(result.Success);
            foreach (var cell in result.Data.Cells)
            {
                Assert.IsNotNull(cell.Center);
                Assert.AreEqual(2, cell.Center.Length);
                Assert.GreaterOrEqual(cell.Center[0], 0f);
                Assert.LessOrEqual(cell.Center[0], 100f);
                Assert.GreaterOrEqual(cell.Center[1], 0f);
                Assert.LessOrEqual(cell.Center[1], 100f);
            }
        }

        // =================================================================
        // Delaunay Tests
        // =================================================================

        [Test]
        public void Delaunay_ValidTriangulation_CoversAllPoints()
        {
            var pts = new[]
            {
                new[] { 0f, 0f },
                new[] { 10f, 0f },
                new[] { 5f, 10f },
                new[] { 10f, 10f },
                new[] { 0f, 10f }
            };

            var result = ProcGenDelaunayTool.Execute(new ProcGenDelaunayParams
            {
                Points     = pts,
                CreateMesh = false
            });

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.Greater(result.Data.TriangleCount, 0);
            Assert.AreEqual(5, result.Data.VertexCount);
            Assert.AreEqual(result.Data.TriangleCount * 3, result.Data.Triangles.Length);

            // All triangle indices should be valid
            foreach (int idx in result.Data.Triangles)
            {
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, pts.Length);
            }

            // All points should appear in at least one triangle
            var usedVertices = new HashSet<int>(result.Data.Triangles);
            for (int i = 0; i < pts.Length; i++)
                Assert.IsTrue(usedVertices.Contains(i), $"Point {i} not in any triangle");
        }

        [Test]
        public void Delaunay_MinimumPoints_ThreePointsOneTriangle()
        {
            var pts = new[]
            {
                new[] { 0f, 0f },
                new[] { 10f, 0f },
                new[] { 5f, 10f }
            };

            var result = ProcGenDelaunayTool.Execute(new ProcGenDelaunayParams
            {
                Points     = pts,
                CreateMesh = false
            });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Data.TriangleCount);
        }

        [Test]
        public void Delaunay_TooFewPoints_ReturnsError()
        {
            var result = ProcGenDelaunayTool.Execute(new ProcGenDelaunayParams
            {
                Points     = new[] { new[] { 0f, 0f }, new[] { 1f, 1f } },
                CreateMesh = false
            });
            Assert.IsFalse(result.Success);

            // Null points
            result = ProcGenDelaunayTool.Execute(new ProcGenDelaunayParams
            {
                Points     = null,
                CreateMesh = false
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Delaunay_DuplicatePoints_ReturnsError()
        {
            var result = ProcGenDelaunayTool.Execute(new ProcGenDelaunayParams
            {
                Points = new[]
                {
                    new[] { 5f, 5f },
                    new[] { 5f, 5f },
                    new[] { 10f, 10f }
                },
                CreateMesh = false
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Delaunay_InvalidPoint_ReturnsError()
        {
            var result = ProcGenDelaunayTool.Execute(new ProcGenDelaunayParams
            {
                Points = new[]
                {
                    new[] { 0f, 0f },
                    new[] { 10f },  // too short
                    new[] { 5f, 10f }
                },
                CreateMesh = false
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Delaunay_InvalidSavePath_ReturnsError()
        {
            var result = ProcGenDelaunayTool.Execute(new ProcGenDelaunayParams
            {
                Points = new[]
                {
                    new[] { 0f, 0f },
                    new[] { 10f, 0f },
                    new[] { 5f, 10f }
                },
                CreateMesh = true,
                SavePath   = "NotAssets/Bad"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Delaunay_CreateMeshFalse_NoMeshPath()
        {
            var result = ProcGenDelaunayTool.Execute(new ProcGenDelaunayParams
            {
                Points = new[]
                {
                    new[] { 0f, 0f },
                    new[] { 10f, 0f },
                    new[] { 5f, 10f }
                },
                CreateMesh = false
            });

            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Data.MeshPath);
            Assert.IsNull(result.Data.GameObjectName);
        }

        [Test]
        public void Delaunay_LargerPointSet_ProducesValidTriangulation()
        {
            var rng = new System.Random(99);
            var pts = new float[30][];
            for (int i = 0; i < 30; i++)
                pts[i] = new[] { (float)(rng.NextDouble() * 100), (float)(rng.NextDouble() * 100) };

            var result = ProcGenDelaunayTool.Execute(new ProcGenDelaunayParams
            {
                Points     = pts,
                CreateMesh = false
            });

            Assert.IsTrue(result.Success, $"Expected success but got: {result.Error}");
            Assert.AreEqual(30, result.Data.VertexCount);
            Assert.Greater(result.Data.TriangleCount, 0);

            // Verify all indices valid
            foreach (int idx in result.Data.Triangles)
            {
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, 30);
            }
        }

        // =================================================================
        // Helpers
        // =================================================================

        private static float ComputeNeighborVariance(VoronoiCellInfo[] cells)
        {
            if (cells.Length == 0) return 0;
            float sum = 0;
            foreach (var c in cells)
                sum += c.NeighborCount;
            float mean = sum / cells.Length;

            float variance = 0;
            foreach (var c in cells)
            {
                float diff = c.NeighborCount - mean;
                variance += diff * diff;
            }
            return variance / cells.Length;
        }
    }
}
