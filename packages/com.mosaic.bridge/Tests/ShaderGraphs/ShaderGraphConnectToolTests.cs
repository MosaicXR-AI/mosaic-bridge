using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.ShaderGraphs;

namespace Mosaic.Bridge.Tests.ShaderGraphs
{
    [TestFixture]
    [Category("ShaderGraph")]
    public class ShaderGraphConnectToolTests
    {
        private const string GraphPath = "Assets/Tests/TestGraph_Connect.shadergraph";

        [SetUp]
        public void SetUp()
        {
            ShaderGraphCreateTool.Execute(new ShaderGraphCreateParams
            {
                Name = "TestGraph_Connect",
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
        public void Connect_TwoNodes_EdgeCreated()
        {
            var floatNode = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                GraphPath = GraphPath, NodeType = "float"
            });
            var multiplyNode = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                GraphPath = GraphPath, NodeType = "multiply"
            });
            Assert.IsTrue(floatNode.Success, floatNode.Error);
            Assert.IsTrue(multiplyNode.Success, multiplyNode.Error);

            // Float Out (slot 0) → Multiply A (slot 0)
            var connect = ShaderGraphConnectTool.Execute(new ShaderGraphConnectParams
            {
                GraphPath    = GraphPath,
                OutputNodeId = floatNode.Data.NodeId,
                OutputSlotId = 0,
                InputNodeId  = multiplyNode.Data.NodeId,
                InputSlotId  = 0
            });
            Assert.IsTrue(connect.Success, connect.Error);
            Assert.AreEqual(1, connect.Data.TotalEdges);
        }

        [Test]
        public void Connect_NonExistentOutputNode_ReturnsFail()
        {
            var targetNode = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                GraphPath = GraphPath, NodeType = "multiply"
            });
            Assert.IsTrue(targetNode.Success, targetNode.Error);

            var result = ShaderGraphConnectTool.Execute(new ShaderGraphConnectParams
            {
                GraphPath    = GraphPath,
                OutputNodeId = "00000000000000000000000000000000",
                OutputSlotId = 0,
                InputNodeId  = targetNode.Data.NodeId,
                InputSlotId  = 0
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Connect_NonExistentInputNode_ReturnsFail()
        {
            var sourceNode = ShaderGraphAddNodeTool.Execute(new ShaderGraphAddNodeParams
            {
                GraphPath = GraphPath, NodeType = "float"
            });
            Assert.IsTrue(sourceNode.Success, sourceNode.Error);

            var result = ShaderGraphConnectTool.Execute(new ShaderGraphConnectParams
            {
                GraphPath    = GraphPath,
                OutputNodeId = sourceNode.Data.NodeId,
                OutputSlotId = 0,
                InputNodeId  = "00000000000000000000000000000000",
                InputSlotId  = 0
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Connect_MissingGraphPath_ReturnsInvalidParam()
        {
            var result = ShaderGraphConnectTool.Execute(new ShaderGraphConnectParams
            {
                OutputNodeId = "abc", OutputSlotId = 0,
                InputNodeId  = "def", InputSlotId  = 0
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
