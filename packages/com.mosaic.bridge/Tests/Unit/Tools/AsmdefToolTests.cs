using System.IO;
using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.AssemblyDefs;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    [TestFixture]
    [Category("Unit")]
    public class AsmdefToolTests
    {
        private const string TestDir = "Assets/_MosaicTestAsmdef";
        private const string TestName = "Mosaic.Test.Asmdef";

        [TearDown]
        public void TearDown()
        {
            var fullDir = Path.GetFullPath(TestDir);
            if (Directory.Exists(fullDir))
            {
                AssetDatabase.DeleteAsset(TestDir);
            }
        }

        [Test]
        public void AsmdefCreate_CreatesValidJson()
        {
            // Ensure the test directory exists
            var fullDir = Path.GetFullPath(TestDir);
            if (!Directory.Exists(fullDir))
                Directory.CreateDirectory(fullDir);

            var result = AsmdefCreateTool.Create(new AsmdefCreateParams
            {
                Name = TestName,
                Path = TestDir,
                References = new[] { "Unity.TextMeshPro" },
                RootNamespace = "Mosaic.Test"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(TestName, result.Data.Name);
            Assert.AreEqual(1, result.Data.ReferenceCount);

            // Verify JSON file exists
            var filePath = Path.Combine(TestDir, TestName + ".asmdef");
            var fullPath = Path.GetFullPath(filePath);
            Assert.IsTrue(File.Exists(fullPath), $"File should exist at {fullPath}");

            // Verify JSON content
            var json = File.ReadAllText(fullPath);
            Assert.IsTrue(json.Contains("\"name\": \"Mosaic.Test.Asmdef\""));
            Assert.IsTrue(json.Contains("Unity.TextMeshPro"));
        }

        [Test]
        public void AsmdefManage_List_ReturnsResults()
        {
            var result = AsmdefManageTool.Manage(new AsmdefManageParams
            {
                Action = "list"
            });

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data.AllPaths);
            Assert.IsTrue(result.Data.AllPaths.Length > 0, "Project should have at least one asmdef");
        }
    }
}
