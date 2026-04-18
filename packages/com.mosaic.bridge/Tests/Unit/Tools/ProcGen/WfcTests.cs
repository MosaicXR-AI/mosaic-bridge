using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mosaic.Bridge.Tools.ProcGen;

namespace Mosaic.Bridge.Tests.Unit.Tools.ProcGen
{
    [TestFixture]
    [Category("Unit")]
    public class WfcTests
    {
        /// <summary>
        /// Helper: creates two tile types ("A" and "B") that can be neighbors in all directions.
        /// </summary>
        static List<TileDefinition> TwoCompatibleTiles()
        {
            var dirs = new Dictionary<string, string[]>
            {
                { "right",   new[] { "A", "B" } },
                { "left",    new[] { "A", "B" } },
                { "up",      new[] { "A", "B" } },
                { "down",    new[] { "A", "B" } },
            };
            return new List<TileDefinition>
            {
                new TileDefinition { Id = "A", Weight = 1f, AllowedNeighbors = dirs },
                new TileDefinition { Id = "B", Weight = 1f, AllowedNeighbors = dirs },
            };
        }

        [Test]
        public void Simple2x2_CollapsesSuccessfully()
        {
            var result = ProcGenWfcTool.Execute(new ProcGenWfcParams
            {
                Width  = 2,
                Height = 2,
                Depth  = 1,
                Seed   = 42,
                Tiles  = TwoCompatibleTiles()
            });

            Assert.IsTrue(result.Success, "Tool call should succeed");
            Assert.IsTrue(result.Data.Success, "WFC should fully collapse");
            Assert.AreEqual(4, result.Data.TotalCells);
            Assert.AreEqual(4, result.Data.CollapsedCells);

            // Every cell should contain exactly one tile
            foreach (var cell in result.Data.Grid)
            {
                Assert.AreEqual(1, cell.Length, "Each cell should have exactly 1 tile");
                Assert.IsTrue(cell[0] == "A" || cell[0] == "B");
            }
        }

        [Test]
        public void AdjacencyConstraints_SatisfiedInOutput()
        {
            // A can only have B to its right; B can only have A to its left
            // For a 3x1 grid this forces A-B-A or B-A-B pattern
            var tileA = new TileDefinition
            {
                Id = "A", Weight = 1f,
                AllowedNeighbors = new Dictionary<string, string[]>
                {
                    { "right", new[] { "B" } },
                    { "left",  new[] { "B" } },
                    { "up",    new[] { "A", "B" } },
                    { "down",  new[] { "A", "B" } },
                }
            };
            var tileB = new TileDefinition
            {
                Id = "B", Weight = 1f,
                AllowedNeighbors = new Dictionary<string, string[]>
                {
                    { "right", new[] { "A" } },
                    { "left",  new[] { "A" } },
                    { "up",    new[] { "A", "B" } },
                    { "down",  new[] { "A", "B" } },
                }
            };

            var result = ProcGenWfcTool.Execute(new ProcGenWfcParams
            {
                Width  = 4,
                Height = 1,
                Depth  = 1,
                Seed   = 7,
                Tiles  = new List<TileDefinition> { tileA, tileB }
            });

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Data.Success);

            // Verify adjacency: each cell's right neighbor must obey constraint
            var grid = result.Data.Grid;
            for (int x = 0; x < 3; x++)
            {
                string current = grid[x][0];
                string right   = grid[x + 1][0];
                if (current == "A")
                    Assert.AreEqual("B", right, $"Cell {x} is A, right neighbor must be B");
                else
                    Assert.AreEqual("A", right, $"Cell {x} is B, right neighbor must be A");
            }
        }

        [Test]
        public void Deterministic_WithSameSeed()
        {
            var p = new ProcGenWfcParams
            {
                Width  = 5,
                Height = 5,
                Depth  = 1,
                Seed   = 123,
                Tiles  = TwoCompatibleTiles()
            };

            var r1 = ProcGenWfcTool.Execute(p);
            var r2 = ProcGenWfcTool.Execute(p);

            Assert.IsTrue(r1.Data.Success);
            Assert.IsTrue(r2.Data.Success);

            for (int i = 0; i < r1.Data.Grid.Length; i++)
            {
                CollectionAssert.AreEqual(r1.Data.Grid[i], r2.Data.Grid[i],
                    $"Cell {i} should match between runs with same seed");
            }
        }

        [Test]
        public void Contradiction_DetectedWithImpossibleConstraints()
        {
            // Two tiles that each ONLY allow themselves as neighbors,
            // but in a 2x2 grid both must appear (since propagation forces it).
            // A: right=A, left=A, up=A, down=A (only allows A everywhere)
            // B: right=B, left=B, up=B, down=B (only allows B everywhere)
            // In a 2x2 grid: if cell0=A, all neighbors must be A. But cell count > 1
            // means the grid CAN be all-A. This isn't contradictory.
            //
            // TRUE contradiction: A only allows B next to it, B only allows A.
            // In a 3-cell row: A-B-A works. But in a 2x2 grid:
            //   A B    A needs right=B (ok), down=B (ok)
            //   B A    B needs right=A (ok), down=A (ok)
            // That actually works as a checkerboard! Need 3x1:
            // A-B-? : cell2 must be A (B allows only A to right).
            // But A needs left=B (ok). So A-B-A works for 3x1.
            //
            // Real contradiction: 3 tiles, A allows only B right, B allows only C right,
            // C allows only A right. In a 2x1 grid, if cell0=A then cell1=B,
            // but cell1 needs left=?? A only allows B to left? No...
            //
            // Simplest: A allows ONLY B to the right. B allows NOTHING to the right.
            // 3x1 grid: cell0=A, cell1=B, cell2=??? B allows nothing right, contradiction.
            var tileA = new TileDefinition
            {
                Id = "A", Weight = 1f,
                AllowedNeighbors = new Dictionary<string, string[]>
                {
                    { "right", new[] { "B" } },
                    { "left",  new[] { "A", "B" } },
                    { "up",    new[] { "A", "B" } },
                    { "down",  new[] { "A", "B" } },
                }
            };
            var tileB = new TileDefinition
            {
                Id = "B", Weight = 1f,
                AllowedNeighbors = new Dictionary<string, string[]>
                {
                    { "right", new string[0] }, // B allows NOTHING to the right
                    { "left",  new[] { "A" } },
                    { "up",    new[] { "A", "B" } },
                    { "down",  new[] { "A", "B" } },
                }
            };

            // 3x1: cell0 can be A or B. If A, cell1 must be B (A only allows B right).
            // cell1=B, cell2=??? B allows nothing right — but cell2 has no right neighbor.
            // So cell2 just needs to satisfy cell1's right constraint... wait, cell2 IS
            // to the right of cell1. B allows nothing right => cell2 gets all eliminated => contradiction.
            // But cell2 has no RIGHT neighbor itself, the issue is cell1 constraining cell2.
            // cell1=B, B allows nothing to the right => cell2 must be in {} => empty => contradiction!
            var result = ProcGenWfcTool.Execute(new ProcGenWfcParams
            {
                Width  = 3,
                Height = 1,
                Depth  = 1,
                Seed   = 42,
                BacktrackLimit = 100,
                Tiles  = new List<TileDefinition> { tileA, tileB }
            });

            Assert.IsTrue(result.Success, "Tool call itself should succeed");
            Assert.IsFalse(result.Data.Success, "WFC should report failure (contradiction)");
        }

        [Test]
        public void BacktrackLimit_Respected()
        {
            // Same impossible constraints, but very low limit
            var tileA = new TileDefinition
            {
                Id = "A", Weight = 1f,
                AllowedNeighbors = new Dictionary<string, string[]>
                {
                    { "right", new[] { "B" } },
                    { "left",  new string[0] },
                    { "up",    new string[0] },
                    { "down",  new string[0] },
                }
            };
            var tileB = new TileDefinition
            {
                Id = "B", Weight = 1f,
                AllowedNeighbors = new Dictionary<string, string[]>
                {
                    { "right", new string[0] },
                    { "left",  new string[0] },
                    { "up",    new string[0] },
                    { "down",  new string[0] },
                }
            };

            var result = ProcGenWfcTool.Execute(new ProcGenWfcParams
            {
                Width  = 4,
                Height = 4,
                Depth  = 1,
                Seed   = 1,
                BacktrackLimit = 5,
                Tiles  = new List<TileDefinition> { tileA, tileB }
            });

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Data.Success);
            Assert.LessOrEqual(result.Data.Backtracks, 5,
                "Should not exceed backtrack limit");
        }

        [Test]
        public void ThreeDimensional_DepthGreaterThanOne()
        {
            var dirs6 = new Dictionary<string, string[]>
            {
                { "right",   new[] { "A", "B" } },
                { "left",    new[] { "A", "B" } },
                { "up",      new[] { "A", "B" } },
                { "down",    new[] { "A", "B" } },
                { "forward", new[] { "A", "B" } },
                { "back",    new[] { "A", "B" } },
            };

            var result = ProcGenWfcTool.Execute(new ProcGenWfcParams
            {
                Width  = 3,
                Height = 3,
                Depth  = 3,
                Seed   = 99,
                Tiles  = new List<TileDefinition>
                {
                    new TileDefinition { Id = "A", Weight = 1f, AllowedNeighbors = dirs6 },
                    new TileDefinition { Id = "B", Weight = 2f, AllowedNeighbors = dirs6 },
                }
            });

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Data.Success);
            Assert.AreEqual(3, result.Data.Depth);
            Assert.AreEqual(27, result.Data.TotalCells);
            Assert.AreEqual(27, result.Data.CollapsedCells);

            foreach (var cell in result.Data.Grid)
            {
                Assert.AreEqual(1, cell.Length);
                Assert.IsTrue(cell[0] == "A" || cell[0] == "B");
            }
        }
    }
}
