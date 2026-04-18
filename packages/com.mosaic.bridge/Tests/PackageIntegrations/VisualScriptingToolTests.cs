#if MOSAIC_HAS_VISUALSCRIPTING
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.VisualScripting;

namespace Mosaic.Bridge.Tests.PackageIntegrations
{
    [TestFixture]
    [Category("PackageIntegration")]
    public class VisualScriptingToolTests
    {
        private const string TestGraphPath = "Assets/MosaicTestTemp/TestGraph.asset";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(TestGraphPath) != null)
                AssetDatabase.DeleteAsset(TestGraphPath);

            var go = GameObject.Find("VS_TestGO");
            if (go != null) Object.DestroyImmediate(go);

            AssetDatabase.Refresh();
        }

        [Test]
        public void CreateGraph_BasicGraph_ReturnsSuccess()
        {
            var result = VisualScriptingCreateGraphTool.Execute(new VisualScriptingCreateGraphParams
            {
                Path = TestGraphPath,
                Name = "TestGraph"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Object>(TestGraphPath));
        }

        [Test]
        public void CreateGraph_InvalidPath_ReturnsFail()
        {
            var result = VisualScriptingCreateGraphTool.Execute(new VisualScriptingCreateGraphParams
            {
                Path = "NotAssets/bad.asset"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void CreateGraph_AttachToGO_AddsMachine()
        {
            var go = new GameObject("VS_TestGO");

            var result = VisualScriptingCreateGraphTool.Execute(new VisualScriptingCreateGraphParams
            {
                Path = TestGraphPath,
                Name = "TestGraph",
                AttachTo = "VS_TestGO"
            });
            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void AddNode_NoGraph_ReturnsFail()
        {
            var result = VisualScriptingAddNodeTool.Execute(new VisualScriptingAddNodeParams
            {
                GraphPath = "Assets/NonExistent.asset",
                NodeType = "Debug.Log"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void AddNode_AfterCreate_ReturnsSuccess()
        {
            VisualScriptingCreateGraphTool.Execute(new VisualScriptingCreateGraphParams
            {
                Path = TestGraphPath, Name = "TestGraph"
            });

            var result = VisualScriptingAddNodeTool.Execute(new VisualScriptingAddNodeParams
            {
                GraphPath = TestGraphPath,
                NodeType = "Debug.Log"
            });
            Assert.IsTrue(result.Success, result.Error);
        }
    }
}
#endif
