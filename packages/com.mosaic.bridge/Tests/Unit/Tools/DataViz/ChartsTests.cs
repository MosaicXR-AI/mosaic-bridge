using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.DataViz;

namespace Mosaic.Bridge.Tests.Unit.Tools.DataViz
{
    [TestFixture]
    [Category("Unit")]
    public class ChartsTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private GameObject ResolveGO(int id)
        {
#pragma warning disable CS0618
            return EditorUtility.InstanceIDToObject(id) as GameObject;
#pragma warning restore CS0618
        }

        private GameObject Track(int id)
        {
            var go = ResolveGO(id);
            if (go != null) _spawned.Add(go);
            return go;
        }

        private static int CountChildrenNamed(GameObject root, string prefix)
        {
            int n = 0;
            foreach (Transform t in root.transform)
                if (t.name.StartsWith(prefix)) n++;
            return n;
        }

        // -----------------------------------------------------------------
        // Scatter
        // -----------------------------------------------------------------
        [Test]
        public void Scatter_TenPoints_CreatesParentWithTenSpheres()
        {
            var pts = new List<ChartScatterParams.Point>();
            for (int i = 0; i < 10; i++)
                pts.Add(new ChartScatterParams.Point
                {
                    Position = new float[] { i, i * 0.5f, -i }
                });

            var result = ChartScatterTool.Execute(new ChartScatterParams
            {
                Points = pts,
                ShowAxes = false,
                ShowGrid = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(10, result.Data.PointCount);

            var parent = Track(result.Data.InstanceId);
            Assert.IsNotNull(parent);
            Assert.AreEqual(10, CountChildrenNamed(parent, "Point_"));
        }

        [Test]
        public void Scatter_EmptyPoints_ReturnsError()
        {
            var result = ChartScatterTool.Execute(new ChartScatterParams
            {
                Points = new List<ChartScatterParams.Point>()
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("non-empty"));
        }

        [Test]
        public void Scatter_NullPoints_ReturnsError()
        {
            var result = ChartScatterTool.Execute(new ChartScatterParams { Points = null });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Scatter_AutoScale_NormalizesIntoUnitCube()
        {
            var pts = new List<ChartScatterParams.Point>
            {
                new ChartScatterParams.Point { Position = new float[] { 0f,   0f,   0f   } },
                new ChartScatterParams.Point { Position = new float[] { 100f, 200f, 300f } },
                new ChartScatterParams.Point { Position = new float[] { 50f,  100f, 150f } },
            };

            var result = ChartScatterTool.Execute(new ChartScatterParams
            {
                Points   = pts,
                AutoScale = true,
                ShowAxes = false
            });

            Assert.IsTrue(result.Success, result.Error);
            var parent = Track(result.Data.InstanceId);
            Assert.IsNotNull(parent);

            // Every point must lie inside [0,10] on each axis.
            foreach (Transform child in parent.transform)
            {
                if (!child.name.StartsWith("Point_")) continue;
                var p = child.localPosition;
                Assert.GreaterOrEqual(p.x, -0.001f);
                Assert.LessOrEqual   (p.x, 10.001f);
                Assert.GreaterOrEqual(p.y, -0.001f);
                Assert.LessOrEqual   (p.y, 10.001f);
                Assert.GreaterOrEqual(p.z, -0.001f);
                Assert.LessOrEqual   (p.z, 10.001f);
            }
        }

        // -----------------------------------------------------------------
        // Bar
        // -----------------------------------------------------------------
        [Test]
        public void Bar_FiveBars_CreatesFiveCubes()
        {
            var bars = new List<ChartBarParams.Bar>
            {
                new ChartBarParams.Bar { Label = "A", Value = 1f },
                new ChartBarParams.Bar { Label = "B", Value = 2f },
                new ChartBarParams.Bar { Label = "C", Value = 3f },
                new ChartBarParams.Bar { Label = "D", Value = 4f },
                new ChartBarParams.Bar { Label = "E", Value = 5f },
            };

            var result = ChartBarTool.Execute(new ChartBarParams
            {
                Bars = bars,
                ShowAxes = false,
                ShowLabels = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(5, result.Data.BarCount);

            var parent = Track(result.Data.InstanceId);
            Assert.IsNotNull(parent);
            Assert.AreEqual(5, CountChildrenNamed(parent, "Bar_"));
        }

        [Test]
        public void Bar_EmptyBars_ReturnsError()
        {
            var result = ChartBarTool.Execute(new ChartBarParams
            {
                Bars = new List<ChartBarParams.Bar>()
            });
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("non-empty"));
        }

        [Test]
        public void Bar_AutoScale_TallestMatchesMaxHeight()
        {
            var bars = new List<ChartBarParams.Bar>
            {
                new ChartBarParams.Bar { Label = "A", Value = 50f },
                new ChartBarParams.Bar { Label = "B", Value = 100f }, // tallest
                new ChartBarParams.Bar { Label = "C", Value = 25f  },
            };

            var result = ChartBarTool.Execute(new ChartBarParams
            {
                Bars       = bars,
                MaxHeight  = 10f,
                ShowAxes   = false,
                ShowLabels = false
            });

            Assert.IsTrue(result.Success, result.Error);
            var parent = Track(result.Data.InstanceId);

            // Find tallest cube by scale.y
            float tallest = 0f;
            foreach (Transform t in parent.transform)
            {
                if (!t.name.StartsWith("Bar_")) continue;
                if (t.localScale.y > tallest) tallest = t.localScale.y;
            }
            Assert.AreEqual(10f, tallest, 0.01f,
                "Tallest bar height should match MaxHeight after normalization");
        }

        // -----------------------------------------------------------------
        // Network
        // -----------------------------------------------------------------
        [Test]
        public void Network_ThreeNodesTwoEdges_CreatesCorrectCounts()
        {
            var nodes = new List<ChartNetworkParams.Node>
            {
                new ChartNetworkParams.Node { Id = "a", Label = "A", Position = new float[] { 0f, 0f, 0f } },
                new ChartNetworkParams.Node { Id = "b", Label = "B", Position = new float[] { 1f, 0f, 0f } },
                new ChartNetworkParams.Node { Id = "c", Label = "C", Position = new float[] { 2f, 0f, 0f } },
            };
            var edges = new List<ChartNetworkParams.Edge>
            {
                new ChartNetworkParams.Edge { From = "a", To = "b" },
                new ChartNetworkParams.Edge { From = "b", To = "c" },
            };

            var result = ChartNetworkTool.Execute(new ChartNetworkParams
            {
                Nodes = nodes,
                Edges = edges,
                AutoLayout = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.NodeCount);
            Assert.AreEqual(2, result.Data.EdgeCount);

            var parent = Track(result.Data.InstanceId);
            Assert.IsNotNull(parent);
            Assert.AreEqual(3, CountChildrenNamed(parent, "Node_"));
            Assert.AreEqual(2, CountChildrenNamed(parent, "Edge_"));
        }

        [Test]
        public void Network_EmptyNodes_ReturnsError()
        {
            var result = ChartNetworkTool.Execute(new ChartNetworkParams
            {
                Nodes = new List<ChartNetworkParams.Node>()
            });
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("non-empty"));
        }

        [Test]
        public void Network_MissingFromNode_ReturnsError()
        {
            var nodes = new List<ChartNetworkParams.Node>
            {
                new ChartNetworkParams.Node { Id = "a", Label = "A" },
                new ChartNetworkParams.Node { Id = "b", Label = "B" },
            };
            var edges = new List<ChartNetworkParams.Edge>
            {
                new ChartNetworkParams.Edge { From = "ghost", To = "a" },
            };

            var result = ChartNetworkTool.Execute(new ChartNetworkParams
            {
                Nodes = nodes,
                Edges = edges
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("ghost"));
        }

        [Test]
        public void Network_MissingToNode_ReturnsError()
        {
            var nodes = new List<ChartNetworkParams.Node>
            {
                new ChartNetworkParams.Node { Id = "a", Label = "A" },
            };
            var edges = new List<ChartNetworkParams.Edge>
            {
                new ChartNetworkParams.Edge { From = "a", To = "ghost" },
            };

            var result = ChartNetworkTool.Execute(new ChartNetworkParams
            {
                Nodes = nodes,
                Edges = edges
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("ghost"));
        }

        [Test]
        public void Network_AutoLayout_ProducesNonZeroPositions()
        {
            var nodes = new List<ChartNetworkParams.Node>
            {
                new ChartNetworkParams.Node { Id = "a", Label = "A" },
                new ChartNetworkParams.Node { Id = "b", Label = "B" },
                new ChartNetworkParams.Node { Id = "c", Label = "C" },
                new ChartNetworkParams.Node { Id = "d", Label = "D" },
            };
            var edges = new List<ChartNetworkParams.Edge>
            {
                new ChartNetworkParams.Edge { From = "a", To = "b" },
                new ChartNetworkParams.Edge { From = "b", To = "c" },
                new ChartNetworkParams.Edge { From = "c", To = "d" },
            };

            var result = ChartNetworkTool.Execute(new ChartNetworkParams
            {
                Nodes = nodes,
                Edges = edges,
                AutoLayout = true,
                LayoutIterations = 50
            });

            Assert.IsTrue(result.Success, result.Error);
            var parent = Track(result.Data.InstanceId);

            // Nodes should not all be at origin after force-directed layout
            var positions = parent.transform
                .Cast<Transform>()
                .Where(t => t.name.StartsWith("Node_"))
                .Select(t => t.localPosition)
                .ToList();

            Assert.AreEqual(4, positions.Count);
            Assert.IsTrue(positions.Any(v => v.sqrMagnitude > 0.001f),
                "At least one node should have non-zero position after auto-layout");
        }
    }
}
