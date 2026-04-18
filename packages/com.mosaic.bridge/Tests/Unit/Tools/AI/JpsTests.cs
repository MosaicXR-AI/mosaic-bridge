using System;
using System.Linq;
using NUnit.Framework;
using Mosaic.Bridge.Tools.AI;

namespace Mosaic.Bridge.Tests.Unit.Tools.AI
{
    [TestFixture]
    [Category("Unit")]
    public class JpsTests
    {
        // -----------------------------------------------------------------
        // Helper
        // -----------------------------------------------------------------
        private static AiPathfindJpsResult Run(AiPathfindJpsParams p, bool expectSuccess = true)
        {
            var result = AiPathfindJpsTool.Execute(p);
            if (expectSuccess)
            {
                Assert.IsTrue(result.Success, $"Expected success but got error: {result.Error}");
                Assert.IsNotNull(result.Data);
                Assert.IsTrue(result.Data.Success, "Result.Data.Success should be true");
                return result.Data;
            }
            // For expected failures, either the tool-level or data-level success is false
            if (result.Success && result.Data != null)
                Assert.IsFalse(result.Data.Success, "Expected no-path but got success");
            return result.Data;
        }

        // -----------------------------------------------------------------
        // 1. Simple path found on open grid
        // -----------------------------------------------------------------
        [Test]
        public void OpenGrid_FindsPath()
        {
            var data = Run(new AiPathfindJpsParams
            {
                GridWidth  = 10,
                GridHeight = 10,
                Start      = new[] { 0, 0 },
                End        = new[] { 9, 9 }
            });

            Assert.IsNotNull(data.Path);
            Assert.Greater(data.Path.Length, 0, "Path should have waypoints");
            Assert.AreEqual(new[] { 0, 0 }, data.Path[0], "Path should start at start");
            Assert.AreEqual(new[] { 9, 9 }, data.Path[data.Path.Length - 1], "Path should end at end");
            Assert.Greater(data.PathLength, 0f);
            Assert.Greater(data.NodesExplored, 0);
        }

        // -----------------------------------------------------------------
        // 2. Path avoids obstacles
        // -----------------------------------------------------------------
        [Test]
        public void PathAvoidsObstacles()
        {
            // Create a wall blocking direct path
            var obstacles = new int[8][];
            for (int i = 0; i < 8; i++)
                obstacles[i] = new[] { 5, i };

            var data = Run(new AiPathfindJpsParams
            {
                GridWidth  = 10,
                GridHeight = 10,
                Start      = new[] { 0, 0 },
                End        = new[] { 9, 0 },
                Obstacles  = obstacles
            });

            // Verify no waypoint in the path is an obstacle
            var obsSet = obstacles.Select(o => (o[0], o[1])).ToHashSet();
            foreach (var wp in data.Path)
                Assert.IsFalse(obsSet.Contains((wp[0], wp[1])),
                    $"Path goes through obstacle at ({wp[0]}, {wp[1]})");
        }

        // -----------------------------------------------------------------
        // 3. No path returns success=false
        // -----------------------------------------------------------------
        [Test]
        public void NoPath_ReturnsSuccessFalse()
        {
            // Completely wall off the end
            var obstacles = new int[10][];
            for (int i = 0; i < 10; i++)
                obstacles[i] = new[] { 5, i };

            var result = AiPathfindJpsTool.Execute(new AiPathfindJpsParams
            {
                GridWidth  = 10,
                GridHeight = 10,
                Start      = new[] { 0, 0 },
                End        = new[] { 9, 0 },
                Obstacles  = obstacles
            });

            Assert.IsTrue(result.Success, "Tool should succeed even when no path found");
            Assert.IsFalse(result.Data.Success, "Data.Success should be false when no path exists");
            Assert.AreEqual(0, result.Data.Path.Length, "Path should be empty");
        }

        // -----------------------------------------------------------------
        // 4. JPS explores fewer nodes than brute-force A*
        // -----------------------------------------------------------------
        [Test]
        public void JPS_ExploresFewerNodesThanBruteForce()
        {
            // On a large open grid, JPS should explore far fewer nodes
            // than the total number of cells an A* would touch
            var data = Run(new AiPathfindJpsParams
            {
                GridWidth  = 50,
                GridHeight = 50,
                Start      = new[] { 0, 0 },
                End        = new[] { 49, 49 }
            });

            // On a 50x50 open grid, standard A* explores ~2500 nodes.
            // JPS should explore significantly fewer (typically < 10 on an open grid)
            Assert.Less(data.NodesExplored, 100,
                $"JPS explored {data.NodesExplored} nodes on a 50x50 open grid — expected far fewer");
        }

        // -----------------------------------------------------------------
        // 5. Diagonal movement disabled works
        // -----------------------------------------------------------------
        [Test]
        public void DiagonalDisabled_FindsCardinalPath()
        {
            // Cardinal-only JPS: use same column so jump finds goal directly
            var data = Run(new AiPathfindJpsParams
            {
                GridWidth        = 10,
                GridHeight       = 10,
                Start            = new[] { 0, 0 },
                End              = new[] { 0, 5 },
                DiagonalMovement = false
            });

            Assert.IsNotNull(data.Path);
            Assert.Greater(data.Path.Length, 0);

            // Verify only cardinal moves (each step differs by exactly 1 in x or y, not both)
            for (int i = 1; i < data.Path.Length; i++)
            {
                int dxAbs = Math.Abs(data.Path[i][0] - data.Path[i - 1][0]);
                int dyAbs = Math.Abs(data.Path[i][1] - data.Path[i - 1][1]);
                // JPS returns jump points, so consecutive waypoints may be far apart
                // but the direction must be purely cardinal (one axis zero)
                Assert.IsTrue(dxAbs == 0 || dyAbs == 0,
                    $"Step {i - 1}->{i}: dx={dxAbs}, dy={dyAbs} — expected cardinal movement only");
            }

            // Manhattan distance should match path length for cardinal-only on open grid
            float expectedLength = Math.Abs(0 - 0) + Math.Abs(5 - 0);
            Assert.AreEqual(expectedLength, data.PathLength, 0.01f,
                "Cardinal path length should equal Manhattan distance on open grid");
        }

        // -----------------------------------------------------------------
        // 6. Start == End returns empty path
        // -----------------------------------------------------------------
        [Test]
        public void StartEqualsEnd_ReturnsEmptyPath()
        {
            var data = Run(new AiPathfindJpsParams
            {
                GridWidth  = 10,
                GridHeight = 10,
                Start      = new[] { 5, 5 },
                End        = new[] { 5, 5 }
            });

            Assert.AreEqual(0, data.Path.Length, "Path should be empty when start == end");
            Assert.AreEqual(0f, data.PathLength);
            Assert.AreEqual(0, data.NodesExplored);
        }

        // -----------------------------------------------------------------
        // 7. Out-of-bounds start/end returns error
        // -----------------------------------------------------------------
        [Test]
        public void OutOfBoundsStart_ReturnsError()
        {
            var result = AiPathfindJpsTool.Execute(new AiPathfindJpsParams
            {
                GridWidth  = 10,
                GridHeight = 10,
                Start      = new[] { -1, 0 },
                End        = new[] { 5, 5 }
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("outside the grid"));
        }

        [Test]
        public void OutOfBoundsEnd_ReturnsError()
        {
            var result = AiPathfindJpsTool.Execute(new AiPathfindJpsParams
            {
                GridWidth  = 10,
                GridHeight = 10,
                Start      = new[] { 0, 0 },
                End        = new[] { 10, 10 }
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("outside the grid"));
        }

        // -----------------------------------------------------------------
        // 8. Manhattan vs Octile both find valid paths
        // -----------------------------------------------------------------
        [Test]
        public void Manhattan_FindsValidPath()
        {
            var data = Run(new AiPathfindJpsParams
            {
                GridWidth  = 20,
                GridHeight = 20,
                Start      = new[] { 0, 0 },
                End        = new[] { 19, 19 },
                Heuristic  = "manhattan"
            });

            Assert.Greater(data.Path.Length, 0);
            Assert.AreEqual(new[] { 0, 0 }, data.Path[0]);
            Assert.AreEqual(new[] { 19, 19 }, data.Path[data.Path.Length - 1]);
        }

        [Test]
        public void Octile_FindsValidPath()
        {
            var data = Run(new AiPathfindJpsParams
            {
                GridWidth  = 20,
                GridHeight = 20,
                Start      = new[] { 0, 0 },
                End        = new[] { 19, 19 },
                Heuristic  = "octile"
            });

            Assert.Greater(data.Path.Length, 0);
            Assert.AreEqual(new[] { 0, 0 }, data.Path[0]);
            Assert.AreEqual(new[] { 19, 19 }, data.Path[data.Path.Length - 1]);
        }

        [Test]
        public void Euclidean_FindsValidPath()
        {
            var data = Run(new AiPathfindJpsParams
            {
                GridWidth  = 20,
                GridHeight = 20,
                Start      = new[] { 0, 0 },
                End        = new[] { 19, 19 },
                Heuristic  = "euclidean"
            });

            Assert.Greater(data.Path.Length, 0);
            Assert.AreEqual(new[] { 0, 0 }, data.Path[0]);
            Assert.AreEqual(new[] { 19, 19 }, data.Path[data.Path.Length - 1]);
        }

        // -----------------------------------------------------------------
        // Edge: invalid heuristic returns error
        // -----------------------------------------------------------------
        [Test]
        public void InvalidHeuristic_ReturnsError()
        {
            var result = AiPathfindJpsTool.Execute(new AiPathfindJpsParams
            {
                GridWidth  = 10,
                GridHeight = 10,
                Start      = new[] { 0, 0 },
                End        = new[] { 5, 5 },
                Heuristic  = "invalid"
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Unknown heuristic"));
        }

        // -----------------------------------------------------------------
        // Edge: start on obstacle returns error
        // -----------------------------------------------------------------
        [Test]
        public void StartOnObstacle_ReturnsError()
        {
            var result = AiPathfindJpsTool.Execute(new AiPathfindJpsParams
            {
                GridWidth  = 10,
                GridHeight = 10,
                Start      = new[] { 0, 0 },
                End        = new[] { 5, 5 },
                Obstacles  = new[] { new[] { 0, 0 } }
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("obstacle"));
        }
    }
}
