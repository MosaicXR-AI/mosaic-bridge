using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.ShaderGraphs;

namespace Mosaic.Bridge.Tests.ShaderGraphs
{
    [TestFixture]
    [Category("ShaderGraph")]
    public class ShaderGraphAddNodeToolTests
    {
        private const string GraphPath = "Assets/Tests/TestGraph_AddNode.shadergraph";

        [SetUp]
        public void SetUp()
        {
            ShaderGraphCreateTool.Execute(new ShaderGraphCreateParams
            {
                Name = "TestGraph_AddNode",
                Path = GraphPath,
                ShaderType = "Unlit",
                OverwriteExisting = true
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.AssetPathExists(GraphPath))
                AssetDatabase.DeleteAsset(GraphPath);
        }

        [Test]
        public void AddNode_MultiplyAlias_ReturnsNodeIdAndSlots()
        {
            var result = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                GraphPath = GraphPath,
                NodeType  = "multiply"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.NodeId));
            Assert.IsNotNull(result.Data.Slots);
            Assert.Greater(result.Data.Slots.Length, 0);
            // Multiply has A, B (input) and Out (output)
            Assert.AreEqual(3, result.Data.Slots.Length);
        }

        [Test]
        public void AddNode_FloatAlias_ReturnsSingleOutputSlot()
        {
            var result = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                GraphPath    = GraphPath,
                NodeType     = "float",
                DefaultValue = 1.5f
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Slots.Length);
            Assert.AreEqual("Output", result.Data.Slots[0].Direction);
        }

        [Test]
        public void AddNode_TwoNodes_IncreasesNodeCount()
        {
            var r1 = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                GraphPath = GraphPath, NodeType = "add"
            });
            var r2 = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                GraphPath = GraphPath, NodeType = "multiply"
            });
            Assert.IsTrue(r1.Success, r1.Error);
            Assert.IsTrue(r2.Success, r2.Error);
            Assert.Less(r1.Data.TotalNodes, r2.Data.TotalNodes);
        }

        [Test]
        public void AddNode_UnknownAlias_ReturnsInvalidParam()
        {
            var result = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                GraphPath = GraphPath,
                NodeType  = "nonexistent_node_xyz"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void AddNode_MissingGraphPath_ReturnsInvalidParam()
        {
            var result = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                NodeType = "multiply"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void AddNode_NonExistentGraph_ReturnsNotFound()
        {
            var result = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                GraphPath = "Assets/Does/Not/Exist.shadergraph",
                NodeType  = "multiply"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}
