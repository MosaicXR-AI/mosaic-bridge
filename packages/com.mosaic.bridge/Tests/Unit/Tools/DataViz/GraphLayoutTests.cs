using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.DataViz;

namespace Mosaic.Bridge.Tests.Unit.Tools.DataViz
{
    /// <summary>
    /// Unit tests for the data/graph-layout tool (Story 34-5).
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("DataViz")]
    public class GraphLayoutTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        void Track(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var go = GameObject.Find(name);
            if (go != null) _spawned.Add(go);
        }

        static List<DataGraphLayoutParams.Node> FiveNodes() => new List<DataGraphLayoutParams.Node>
        {
            new DataGraphLayoutParams.Node { Id = "A" },
            new DataGraphLayoutParams.Node { Id = "B" },
            new DataGraphLayoutParams.Node { Id = "C" },
            new DataGraphLayoutParams.Node { Id = "D" },
            new DataGraphLayoutParams.Node { Id = "E" },
        };

        static List<DataGraphLayoutParams.Edge> FourEdges() => new List<DataGraphLayoutParams.Edge>
        {
            new DataGraphLayoutParams.Edge { From = "A", To = "B" },
            new DataGraphLayoutParams.Edge { From = "B", To = "C" },
            new DataGraphLayoutParams.Edge { From = "C", To = "D" },
            new DataGraphLayoutParams.Edge { From = "D", To = "E" },
        };

        [Test]
        public void ForceDirected_FiveNodesFourEdges_ProducesValidPositions()
        {
            var p = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Edges = FourEdges(),
                Algorithm = "force_directed",
                Iterations = 50,
                Seed = 42,
                CreateVisuals = false,
            };
            var result = DataGraphLayoutTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(5, result.Data.NodeCount);
            Assert.AreEqual(4, result.Data.EdgeCount);
            Assert.AreEqual("force_directed", result.Data.Algorithm);
            Assert.AreEqual(50, result.Data.Iterations);
            Assert.AreEqual(5, result.Data.NodePositions.Count);
            foreach (var np in result.Data.NodePositions)
            {
                Assert.IsNotNull(np.Position);
                Assert.AreEqual(3, np.Position.Length);
                Assert.IsFalse(float.IsNaN(np.Position[0]));
                Assert.IsFalse(float.IsNaN(np.Position[1]));
                Assert.IsFalse(float.IsNaN(np.Position[2]));
            }
        }

        [Test]
        public void Circular_PlacesNodesOnARing()
        {
            var p = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Algorithm = "circular",
                Bounds = new[] { 10f, 10f, 10f },
                CreateVisuals = false,
            };
            var result = DataGraphLayoutTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);

            float expectedRadius = 5f; // max(10,10)/2
            foreach (var np in result.Data.NodePositions)
            {
                float x = np.Position[0];
                float z = np.Position[2];
                float r = Mathf.Sqrt(x * x + z * z);
                Assert.AreEqual(expectedRadius, r, 1e-3f,
                    $"Node {np.Id} radius should match ring radius");
                Assert.AreEqual(0f, np.Position[1], 1e-6f, "Circular layout is flat on XZ");
            }
        }

        [Test]
        public void Grid_SpacesCorrectly()
        {
            var p = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Algorithm = "grid",
                IdealEdgeLength = 2f,
                CreateVisuals = false,
            };
            var result = DataGraphLayoutTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);

            // cols = ceil(sqrt(5)) = 3 → columns at {-2, 0, 2}
            // rows = ceil(5/3)   = 2 → rows    at {-1, 1}
            var pos = result.Data.NodePositions;
            Assert.AreEqual(-2f, pos[0].Position[0], 1e-4f);
            Assert.AreEqual(-1f, pos[0].Position[2], 1e-4f);
            Assert.AreEqual( 0f, pos[1].Position[0], 1e-4f);
            Assert.AreEqual( 2f, pos[2].Position[0], 1e-4f);
            Assert.AreEqual(-2f, pos[3].Position[0], 1e-4f);
            Assert.AreEqual( 1f, pos[3].Position[2], 1e-4f);
            Assert.AreEqual( 0f, pos[4].Position[0], 1e-4f);
            Assert.AreEqual( 1f, pos[4].Position[2], 1e-4f);
        }

        [Test]
        public void EmptyNodes_ReturnsError()
        {
            var result = DataGraphLayoutTool.Execute(new DataGraphLayoutParams
            {
                Nodes = new List<DataGraphLayoutParams.Node>(),
                CreateVisuals = false,
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void NullNodes_ReturnsError()
        {
            var result = DataGraphLayoutTool.Execute(new DataGraphLayoutParams
            {
                Nodes = null,
                CreateVisuals = false,
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void EdgeReferencingMissingNode_ReturnsError()
        {
            var p = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Edges = new List<DataGraphLayoutParams.Edge>
                {
                    new DataGraphLayoutParams.Edge { From = "A", To = "Z" },
                },
                CreateVisuals = false,
            };
            var result = DataGraphLayoutTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void DuplicateNodeId_ReturnsError()
        {
            var p = new DataGraphLayoutParams
            {
                Nodes = new List<DataGraphLayoutParams.Node>
                {
                    new DataGraphLayoutParams.Node { Id = "A" },
                    new DataGraphLayoutParams.Node { Id = "A" },
                },
                CreateVisuals = false,
            };
            var result = DataGraphLayoutTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Deterministic_WithSeed()
        {
            var p1 = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Edges = FourEdges(),
                Algorithm = "force_directed",
                Iterations = 50,
                Seed = 1234,
                CreateVisuals = false,
            };
            var p2 = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Edges = FourEdges(),
                Algorithm = "force_directed",
                Iterations = 50,
                Seed = 1234,
                CreateVisuals = false,
            };
            var r1 = DataGraphLayoutTool.Execute(p1);
            var r2 = DataGraphLayoutTool.Execute(p2);
            Assert.IsTrue(r1.Success);
            Assert.IsTrue(r2.Success);
            for (int i = 0; i < r1.Data.NodePositions.Count; i++)
            {
                Assert.AreEqual(r1.Data.NodePositions[i].Position[0],
                                r2.Data.NodePositions[i].Position[0], 1e-5f);
                Assert.AreEqual(r1.Data.NodePositions[i].Position[1],
                                r2.Data.NodePositions[i].Position[1], 1e-5f);
                Assert.AreEqual(r1.Data.NodePositions[i].Position[2],
                                r2.Data.NodePositions[i].Position[2], 1e-5f);
            }
        }

        [Test]
        public void Layout2D_ForcesYToZero()
        {
            var p = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Edges = FourEdges(),
                Algorithm = "force_directed",
                Iterations = 25,
                Seed = 7,
                Layout3D = false,
                CreateVisuals = false,
            };
            var result = DataGraphLayoutTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            foreach (var np in result.Data.NodePositions)
                Assert.AreEqual(0f, np.Position[1], 1e-6f, $"Node {np.Id} Y must be 0 in 2D layout");
        }

        [Test]
        public void CreateVisualsFalse_ReturnsPositionsOnly_NoGameObject()
        {
            var p = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Edges = FourEdges(),
                Algorithm = "force_directed",
                Iterations = 10,
                Seed = 99,
                CreateVisuals = false,
                Name = "__GraphLayoutTest_NoVisuals__",
            };
            var result = DataGraphLayoutTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(string.IsNullOrEmpty(result.Data.GameObjectName));
            Assert.IsNull(GameObject.Find("__GraphLayoutTest_NoVisuals__"));
            Assert.AreEqual(5, result.Data.NodePositions.Count);
        }

        [Test]
        public void CreateVisualsTrue_SpawnsParentWithNodesAndEdges()
        {
            string name = "__GraphLayoutTest_Visuals_" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
            var p = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Edges = FourEdges(),
                Algorithm = "circular",
                CreateVisuals = true,
                Name = name,
            };
            var result = DataGraphLayoutTool.Execute(p);
            Track(result.Data?.GameObjectName);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(name, result.Data.GameObjectName);
            var parent = GameObject.Find(name);
            Assert.IsNotNull(parent, "Parent GO should exist");
            // 5 nodes + 4 edges
            Assert.AreEqual(9, parent.transform.childCount);
        }

        [Test]
        public void Spring_AlgorithmRuns()
        {
            var p = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Edges = FourEdges(),
                Algorithm = "spring",
                Iterations = 30,
                Seed = 5,
                CreateVisuals = false,
            };
            var result = DataGraphLayoutTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("spring", result.Data.Algorithm);
            Assert.AreEqual(30, result.Data.Iterations);
        }

        [Test]
        public void InvalidAlgorithm_ReturnsError()
        {
            var p = new DataGraphLayoutParams
            {
                Nodes = FiveNodes(),
                Algorithm = "bogus",
                CreateVisuals = false,
            };
            var result = DataGraphLayoutTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
