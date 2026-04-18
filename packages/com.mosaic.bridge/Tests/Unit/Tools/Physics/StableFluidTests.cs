using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Physics;

namespace Mosaic.Bridge.Tests.Unit.Tools.Physics
{
    [TestFixture]
    [Category("Unit")]
    public class StableFluidTests
    {
        private GameObject _createdGo;

        [TearDown]
        public void TearDown()
        {
            if (_createdGo != null)
            {
                Object.DestroyImmediate(_createdGo);
                _createdGo = null;
            }

            // Clean up generated files
            var genPath = "Assets/Generated/Physics/";
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullGenPath = Path.Combine(projectRoot, genPath);
            if (Directory.Exists(fullGenPath))
            {
                Directory.Delete(fullGenPath, true);
                var metaPath = fullGenPath.TrimEnd('/') + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
                AssetDatabase.Refresh();
            }
        }

        private void AssertValidScript(PhysicsFluidCreateResult data, string expectedType, int expectedResolution)
        {
            Assert.IsNotNull(data);
            Assert.AreEqual(expectedType, data.Type);
            Assert.AreEqual(expectedResolution, data.Resolution);
            Assert.IsNotNull(data.ScriptPath);
            Assert.IsTrue(data.ScriptPath.StartsWith("Assets/"), $"ScriptPath should start with 'Assets/' but was '{data.ScriptPath}'");
            Assert.IsTrue(data.ScriptPath.EndsWith(".cs"));
            Assert.AreNotEqual(0, data.InstanceId);

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, data.ScriptPath);
            Assert.IsTrue(File.Exists(fullPath), $"Generated script file does not exist at {fullPath}");
        }

        // ---------------------------------------------------------------------
        // Happy paths
        // ---------------------------------------------------------------------

        [Test]
        public void Create_Smoke_ReturnsValidScript()
        {
            var result = PhysicsFluidCreateTool.Execute(new PhysicsFluidCreateParams
            {
                Type = "smoke",
                Name = "TestSmoke"
            });

            Assert.IsTrue(result.Success, result.Error);
            AssertValidScript(result.Data, "smoke", 64);
            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }

        [Test]
        public void Create_Liquid_ReturnsValidScript()
        {
            var result = PhysicsFluidCreateTool.Execute(new PhysicsFluidCreateParams
            {
                Type = "liquid",
                Name = "TestLiquid"
            });

            Assert.IsTrue(result.Success, result.Error);
            AssertValidScript(result.Data, "liquid", 64);
            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }

        [Test]
        public void Create_Fire_ReturnsValidScript()
        {
            var result = PhysicsFluidCreateTool.Execute(new PhysicsFluidCreateParams
            {
                Type = "fire",
                Name = "TestFire"
            });

            Assert.IsTrue(result.Success, result.Error);
            AssertValidScript(result.Data, "fire", 64);
            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }

        // ---------------------------------------------------------------------
        // Validation
        // ---------------------------------------------------------------------

        [Test]
        public void Create_InvalidType_ReturnsError()
        {
            var result = PhysicsFluidCreateTool.Execute(new PhysicsFluidCreateParams
            {
                Type = "plasma",
                Name = "BadType"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_ResolutionBelowMin_IsClampedToMin()
        {
            var result = PhysicsFluidCreateTool.Execute(new PhysicsFluidCreateParams
            {
                Type       = "smoke",
                Resolution = 4,           // below 8 -> clamp to 8
                Name       = "LowRes"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(8, result.Data.Resolution);
            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }

        [Test]
        public void Create_ResolutionAboveMax_IsClampedToMax()
        {
            var result = PhysicsFluidCreateTool.Execute(new PhysicsFluidCreateParams
            {
                Type       = "smoke",
                Resolution = 512,         // above 128 -> clamp to 128
                Name       = "HighRes"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(128, result.Data.Resolution);
            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }

        [Test]
        public void Create_InvalidResolution_ReturnsError()
        {
            var result = PhysicsFluidCreateTool.Execute(new PhysicsFluidCreateParams
            {
                Type       = "smoke",
                Resolution = -10,
                Name       = "NegRes"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_ScriptPathIsUnderAssets()
        {
            var result = PhysicsFluidCreateTool.Execute(new PhysicsFluidCreateParams
            {
                Type = "smoke",
                Name = "PathCheck"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.ScriptPath.StartsWith("Assets/Generated/Physics/"));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith("StableFluid_PathCheck.cs"));
            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }
    }
}
