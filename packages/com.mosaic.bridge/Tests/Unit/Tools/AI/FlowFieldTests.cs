using System;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.AI;

namespace Mosaic.Bridge.Tests.Unit.Tools.AI
{
    [TestFixture]
    [Category("Unit")]
    public class FlowFieldTests
    {
        // -----------------------------------------------------------------
        // Helper
        // -----------------------------------------------------------------
        private static AiFlowFieldResult Run(AiFlowFieldParams p, bool expectSuccess = true)
        {
            var result = AiFlowFieldTool.Execute(p);
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
        // All reachable cells have non-zero flow direction
        // -----------------------------------------------------------------
        [Test]
        public void ReachableCells_HaveNonZeroFlow()
        {
            var data = Run(new AiFlowFieldParams
            {
                GridWidth  = 5,
                GridHeight = 5,
                Targets    = new[] { new[] { 2, 2 } }
            });

            Assert.AreEqual(5, data.GridWidth);
            Assert.AreEqual(5, data.GridHeight);
            Assert.AreEqual(25, data.FlowField.Length);

            for (int i = 0; i < data.FlowField.Length; i++)
            {
                float dist = data.DistanceField[i];
                float fx = data.FlowField[i][0];
                float fy = data.FlowField[i][1];

                if (dist > 0f && dist < float.MaxValue)
                {
                    float mag = Mathf.Sqrt(fx * fx + fy * fy);
                    Assert.Greater(mag, 0.5f,
                        $"Cell {i % 5},{i / 5} is reachable (dist={dist}) but has zero flow");
                }
            }
        }

        // -----------------------------------------------------------------
        // Flow at target cell is zero vector
        // -----------------------------------------------------------------
        [Test]
        public void TargetCell_HasZeroFlow()
        {
            int tx = 3, ty = 1;
            var data = Run(new AiFlowFieldParams
            {
                GridWidth  = 5,
                GridHeight = 5,
                Targets    = new[] { new[] { tx, ty } }
            });

            int idx = ty * 5 + tx;
            Assert.AreEqual(0f, data.FlowField[idx][0], 0.0001f);
            Assert.AreEqual(0f, data.FlowField[idx][1], 0.0001f);
            Assert.AreEqual(0f, data.DistanceField[idx], 0.0001f);
        }

        // -----------------------------------------------------------------
        // Obstacles are unreachable
        // -----------------------------------------------------------------
        [Test]
        public void Obstacles_AreUnreachable()
        {
            var data = Run(new AiFlowFieldParams
            {
                GridWidth  = 5,
                GridHeight = 5,
                Targets    = new[] { new[] { 0, 0 } },
                Obstacles  = new[] { new[] { 2, 2 }, new[] { 3, 3 } }
            });

            int obsIdx1 = 2 * 5 + 2;
            int obsIdx2 = 3 * 5 + 3;

            Assert.AreEqual(0f, data.FlowField[obsIdx1][0], 0.0001f);
            Assert.AreEqual(0f, data.FlowField[obsIdx1][1], 0.0001f);
            Assert.AreEqual(0f, data.FlowField[obsIdx2][0], 0.0001f);
            Assert.AreEqual(0f, data.FlowField[obsIdx2][1], 0.0001f);

            Assert.Greater(data.UnreachableCells, 0);
        }

        // -----------------------------------------------------------------
        // Flow points toward target (dot product with target direction > 0)
        // -----------------------------------------------------------------
        [Test]
        public void Flow_PointsTowardTarget()
        {
            int tw = 10, th = 10;
            int tx = 5, ty = 5;
            var data = Run(new AiFlowFieldParams
            {
                GridWidth  = tw,
                GridHeight = th,
                Targets    = new[] { new[] { tx, ty } }
            });

            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    int idx = y * tw + x;
                    if (x == tx && y == ty) continue; // skip target

                    float fx = data.FlowField[idx][0];
                    float fy = data.FlowField[idx][1];

                    // Direction to target
                    float dirX = tx - x;
                    float dirY = ty - y;
                    float dirMag = Mathf.Sqrt(dirX * dirX + dirY * dirY);
                    dirX /= dirMag;
                    dirY /= dirMag;

                    float dot = fx * dirX + fy * dirY;
                    Assert.Greater(dot, 0f,
                        $"Cell [{x},{y}] flow ({fx:F2},{fy:F2}) does not point toward target [{tx},{ty}], dot={dot:F3}");
                }
            }
        }

        // -----------------------------------------------------------------
        // Multiple targets create correct combined field
        // -----------------------------------------------------------------
        [Test]
        public void MultipleTargets_CreateCombinedField()
        {
            var data = Run(new AiFlowFieldParams
            {
                GridWidth  = 10,
                GridHeight = 1,
                Targets    = new[] { new[] { 0, 0 }, new[] { 9, 0 } }
            });

            // Cell 0 and 9 are targets (distance 0)
            Assert.AreEqual(0f, data.DistanceField[0], 0.0001f);
            Assert.AreEqual(0f, data.DistanceField[9], 0.0001f);

            // Middle cell (4 or 5) should be equidistant-ish to both targets
            // Cell 4 should be distance 4 from target 0 and distance 5 from target 9
            // The field should pick the closer one: distance = 4
            Assert.AreEqual(4f, data.DistanceField[4], 0.01f);
            // Cell 5 should be distance 5 from target 0 and distance 4 from target 9 => 4
            Assert.AreEqual(4f, data.DistanceField[5], 0.01f);

            // Cell 4 should flow toward target at 0 (left), cell 5 toward target at 9 (right)
            Assert.Less(data.FlowField[4][0], 0f, "Cell 4 should flow left toward target 0");
            Assert.Greater(data.FlowField[5][0], 0f, "Cell 5 should flow right toward target 9");
        }

        // -----------------------------------------------------------------
        // Empty obstacles array works
        // -----------------------------------------------------------------
        [Test]
        public void EmptyObstacles_Works()
        {
            var data = Run(new AiFlowFieldParams
            {
                GridWidth  = 3,
                GridHeight = 3,
                Targets    = new[] { new[] { 1, 1 } },
                Obstacles  = new int[0][]
            });

            Assert.AreEqual(0, data.UnreachableCells);
            Assert.AreEqual(9, data.ReachableCells);
        }

        // -----------------------------------------------------------------
        // Invalid grid size returns error
        // -----------------------------------------------------------------
        [Test]
        public void InvalidGridSize_ReturnsError()
        {
            Run(new AiFlowFieldParams
            {
                GridWidth  = 0,
                GridHeight = 5,
                Targets    = new[] { new[] { 0, 0 } }
            }, expectSuccess: false);

            Run(new AiFlowFieldParams
            {
                GridWidth  = 5,
                GridHeight = -1,
                Targets    = new[] { new[] { 0, 0 } }
            }, expectSuccess: false);
        }

        // -----------------------------------------------------------------
        // No targets returns error
        // -----------------------------------------------------------------
        [Test]
        public void NoTargets_ReturnsError()
        {
            Run(new AiFlowFieldParams
            {
                GridWidth  = 5,
                GridHeight = 5,
                Targets    = new int[0][]
            }, expectSuccess: false);

            Run(new AiFlowFieldParams
            {
                GridWidth  = 5,
                GridHeight = 5,
                Targets    = null
            }, expectSuccess: false);
        }

        // -----------------------------------------------------------------
        // Custom cost field is respected
        // -----------------------------------------------------------------
        [Test]
        public void CustomCostField_AffectsDistances()
        {
            // 3x1 grid: cells 0,1,2. Target at 2.
            // Default costs: dist from 0 = 2
            var dataDefault = Run(new AiFlowFieldParams
            {
                GridWidth  = 3,
                GridHeight = 1,
                Targets    = new[] { new[] { 2, 0 } }
            });

            // High cost on cell 1: dist from 0 should be higher
            var dataHigh = Run(new AiFlowFieldParams
            {
                GridWidth  = 3,
                GridHeight = 1,
                Targets    = new[] { new[] { 2, 0 } },
                CostField  = new[] { 1f, 5f, 1f }
            });

            Assert.Greater(dataHigh.DistanceField[0], dataDefault.DistanceField[0],
                "High-cost cell should increase distance");
        }

        // -----------------------------------------------------------------
        // Target on obstacle returns error
        // -----------------------------------------------------------------
        [Test]
        public void TargetOnObstacle_ReturnsError()
        {
            Run(new AiFlowFieldParams
            {
                GridWidth  = 5,
                GridHeight = 5,
                Targets    = new[] { new[] { 2, 2 } },
                Obstacles  = new[] { new[] { 2, 2 } }
            }, expectSuccess: false);
        }

        // -----------------------------------------------------------------
        // Smoothing produces valid normalized vectors
        // -----------------------------------------------------------------
        [Test]
        public void Smoothing_ProducesNormalizedVectors()
        {
            var data = Run(new AiFlowFieldParams
            {
                GridWidth  = 7,
                GridHeight = 7,
                Targets    = new[] { new[] { 3, 3 } },
                Smoothing  = true
            });

            for (int i = 0; i < data.FlowField.Length; i++)
            {
                float fx = data.FlowField[i][0];
                float fy = data.FlowField[i][1];
                float mag = Mathf.Sqrt(fx * fx + fy * fy);

                if (data.DistanceField[i] > 0f && data.DistanceField[i] < float.MaxValue)
                {
                    Assert.Less(Mathf.Abs(mag - 1f), 0.1f,
                        $"Cell {i % 7},{i / 7} smoothed flow not unit length: mag={mag:F4}");
                }
            }
        }

        // -----------------------------------------------------------------
        // Wrong CostField length returns error
        // -----------------------------------------------------------------
        [Test]
        public void WrongCostFieldLength_ReturnsError()
        {
            Run(new AiFlowFieldParams
            {
                GridWidth  = 5,
                GridHeight = 5,
                Targets    = new[] { new[] { 0, 0 } },
                CostField  = new float[] { 1f, 2f, 3f } // wrong length
            }, expectSuccess: false);
        }

        // -----------------------------------------------------------------
        // Target out of range returns error
        // -----------------------------------------------------------------
        [Test]
        public void TargetOutOfRange_ReturnsError()
        {
            Run(new AiFlowFieldParams
            {
                GridWidth  = 5,
                GridHeight = 5,
                Targets    = new[] { new[] { 10, 10 } }
            }, expectSuccess: false);
        }
    }
}
