using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Mosaic.Bridge.Tools.ShaderGraphs;

namespace Mosaic.Bridge.Tests.Unit.Tools.ShaderGraphs
{
    /// <summary>
    /// Unit tests for ShaderGraph tools.
    /// These tests use temp .shadergraph JSON files on disk rather than requiring
    /// the ShaderGraph package to be installed in the test project.
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("ShaderGraph")]
    public class ShaderGraphToolsTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "MosaicShaderGraphTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        // ── List tool ────────────────────────────────────────────────────────

        [Test]
        public void ShaderGraphList_ReturnsSuccessResult()
        {
            // List may return 0 results in a test project without ShaderGraph assets;
            // we verify the tool does not throw and returns a valid envelope.
            var result = ShaderGraphListTool.Execute(new ShaderGraphListParams());

            Assert.IsTrue(result.Success, "Expected success=true");
            Assert.IsNotNull(result.Data, "Expected non-null data");
            Assert.IsNotNull(result.Data.Graphs, "Expected non-null Graphs list");
            Assert.GreaterOrEqual(result.Data.Count, 0);
            Assert.AreEqual(result.Data.Graphs.Count, result.Data.Count);
        }

        // ── JSON helper tests ────────────────────────────────────────────────

        [Test]
        public void CountNodes_ReturnsCorrectCount()
        {
            var graph = JObject.Parse(@"{
                ""m_SerializedNodes"": [""node1"", ""node2"", ""node3""],
                ""m_SerializedEdges"": [""edge1""]
            }");

            Assert.AreEqual(3, ShaderGraphJsonHelper.CountNodes(graph));
            Assert.AreEqual(1, ShaderGraphJsonHelper.CountEdges(graph));
        }

        [Test]
        public void CountNodes_ReturnsZero_WhenNoNodes()
        {
            var graph = JObject.Parse(@"{}");

            Assert.AreEqual(0, ShaderGraphJsonHelper.CountNodes(graph));
            Assert.AreEqual(0, ShaderGraphJsonHelper.CountEdges(graph));
        }

        [Test]
        public void ExtractProperties_ParsesObjectProperties()
        {
            var graph = JObject.Parse(@"{
                ""m_SerializedProperties"": [
                    {
                        ""m_Name"": ""_BaseColor"",
                        ""m_OverrideReferenceName"": ""_BaseColor"",
                        ""m_Type"": ""Color"",
                        ""m_Value"": ""[1,1,1,1]""
                    },
                    {
                        ""m_Name"": ""_Smoothness"",
                        ""m_OverrideReferenceName"": ""_Smoothness"",
                        ""m_Type"": ""Float"",
                        ""m_Value"": ""0.5""
                    }
                ]
            }");

            var props = ShaderGraphJsonHelper.ExtractProperties(graph);
            Assert.AreEqual(2, props.Count);
            Assert.AreEqual("_BaseColor", props[0].Name);
            Assert.AreEqual("Color", props[0].Type);
            Assert.AreEqual("_Smoothness", props[1].Name);
        }

        [Test]
        public void ExtractProperties_ReturnsEmpty_WhenNoProperties()
        {
            var graph = JObject.Parse(@"{}");

            var props = ShaderGraphJsonHelper.ExtractProperties(graph);
            Assert.AreEqual(0, props.Count);
        }

        [Test]
        public void SetPropertyDefault_UpdatesExistingProperty()
        {
            var graph = JObject.Parse(@"{
                ""m_SerializedProperties"": [
                    {
                        ""m_Name"": ""_Smoothness"",
                        ""m_OverrideReferenceName"": ""_Smoothness"",
                        ""m_Type"": ""Float"",
                        ""m_Value"": ""0.5""
                    }
                ]
            }");

            string oldValue = ShaderGraphJsonHelper.SetPropertyDefault(graph, "_Smoothness", "0.8");

            Assert.IsNotNull(oldValue, "Expected to find property");
            Assert.AreEqual("0.5", oldValue);

            // Verify the new value was set
            var props = ShaderGraphJsonHelper.ExtractProperties(graph);
            Assert.AreEqual(1, props.Count);
            Assert.AreEqual("0.8", props[0].DefaultValue);
        }

        [Test]
        public void SetPropertyDefault_ReturnsNull_WhenPropertyNotFound()
        {
            var graph = JObject.Parse(@"{
                ""m_SerializedProperties"": [
                    {
                        ""m_Name"": ""_Smoothness"",
                        ""m_Type"": ""Float"",
                        ""m_Value"": ""0.5""
                    }
                ]
            }");

            string oldValue = ShaderGraphJsonHelper.SetPropertyDefault(graph, "_NonExistent", "1.0");
            Assert.IsNull(oldValue, "Expected null for non-existent property");
        }

        [Test]
        public void ReadGraph_ReturnsNull_WhenFileDoesNotExist()
        {
            // ReadGraph uses Application.dataPath internally, so we test the null
            // return for a non-existent relative path
            var result = ShaderGraphJsonHelper.ReadGraph("Assets/NonExistent/Missing.shadergraph");
            Assert.IsNull(result);
        }

        // ── Info tool validation ─────────────────────────────────────────────

        [Test]
        public void ShaderGraphInfo_FailsForNullPath()
        {
            var result = ShaderGraphInfoTool.Execute(new ShaderGraphInfoParams { AssetPath = null });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ShaderGraphInfo_FailsForNonShaderGraphExtension()
        {
            var result = ShaderGraphInfoTool.Execute(new ShaderGraphInfoParams { AssetPath = "Assets/Foo.shader" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ShaderGraphInfo_FailsForMissingFile()
        {
            var result = ShaderGraphInfoTool.Execute(
                new ShaderGraphInfoParams { AssetPath = "Assets/NonExistent.shadergraph" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // ── Create tool validation ───────────────────────────────────────────

        [Test]
        public void ShaderGraphCreate_FailsForMissingName()
        {
            var result = ShaderGraphCreateTool.Execute(
                new ShaderGraphCreateParams { Name = null, Path = "Assets/Test.shadergraph" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ShaderGraphCreate_FailsForMissingPath()
        {
            var result = ShaderGraphCreateTool.Execute(
                new ShaderGraphCreateParams { Name = "Test", Path = null });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ShaderGraphCreate_FailsForInvalidShaderType()
        {
            var result = ShaderGraphCreateTool.Execute(
                new ShaderGraphCreateParams
                {
                    Name = "Test",
                    Path = "Assets/Test.shadergraph",
                    ShaderType = "Invalid"
                });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Open tool validation ─────────────────────────────────────────────

        [Test]
        public void ShaderGraphOpen_FailsForNullPath()
        {
            var result = ShaderGraphOpenTool.Execute(new ShaderGraphOpenParams { AssetPath = null });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ShaderGraphOpen_FailsForMissingFile()
        {
            var result = ShaderGraphOpenTool.Execute(
                new ShaderGraphOpenParams { AssetPath = "Assets/NonExistent.shadergraph" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // ── GetProperties tool validation ────────────────────────────────────

        [Test]
        public void ShaderGraphGetProperties_FailsForNullPath()
        {
            var result = ShaderGraphGetPropertiesTool.Execute(
                new ShaderGraphGetPropertiesParams { AssetPath = null });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── SetPropertyDefault tool validation ───────────────────────────────

        [Test]
        public void ShaderGraphSetPropertyDefault_FailsForMissingParams()
        {
            var result = ShaderGraphSetPropertyDefaultTool.Execute(
                new ShaderGraphSetPropertyDefaultParams
                {
                    AssetPath = null,
                    PropertyName = "Foo",
                    Value = "1.0"
                });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);

            result = ShaderGraphSetPropertyDefaultTool.Execute(
                new ShaderGraphSetPropertyDefaultParams
                {
                    AssetPath = "Assets/Test.shadergraph",
                    PropertyName = null,
                    Value = "1.0"
                });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);

            result = ShaderGraphSetPropertyDefaultTool.Execute(
                new ShaderGraphSetPropertyDefaultParams
                {
                    AssetPath = "Assets/Test.shadergraph",
                    PropertyName = "Foo",
                    Value = null
                });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ShaderGraphSetPropertyDefault_FailsForNonShaderGraphExtension()
        {
            var result = ShaderGraphSetPropertyDefaultTool.Execute(
                new ShaderGraphSetPropertyDefaultParams
                {
                    AssetPath = "Assets/Foo.shader",
                    PropertyName = "Foo",
                    Value = "1.0"
                });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
