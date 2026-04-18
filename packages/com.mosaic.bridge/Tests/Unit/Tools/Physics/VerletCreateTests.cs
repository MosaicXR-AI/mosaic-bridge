using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Physics;

namespace Mosaic.Bridge.Tests.Unit.Tools.Physics
{
    [TestFixture]
    [Category("Unit")]
    public class VerletCreateTests
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

        [Test]
        public void Create_Rope_ReturnsValidScriptAndGameObject()
        {
            var result = PhysicsVerletCreateTool.Execute(new PhysicsVerletCreateParams
            {
                Type       = "rope",
                PointCount = 10,
                Name       = "TestRope"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("rope", result.Data.Type);
            Assert.AreEqual(10, result.Data.PointCount);
            Assert.IsNotNull(result.Data.ScriptPath);
            Assert.IsTrue(result.Data.ScriptPath.StartsWith("Assets/"));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith(".cs"));
            Assert.AreEqual("TestRope", result.Data.GameObjectName);
            Assert.AreNotEqual(0, result.Data.InstanceId);

            // Verify file exists on disk
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, result.Data.ScriptPath);
            Assert.IsTrue(File.Exists(fullPath), $"Script file does not exist at {fullPath}");

            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }

        [Test]
        public void Create_Cloth_ReturnsValidScript()
        {
            var result = PhysicsVerletCreateTool.Execute(new PhysicsVerletCreateParams
            {
                Type       = "cloth",
                PointCount = 8, // 8x8 grid -> 64 points
                Name       = "TestCloth"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("cloth", result.Data.Type);
            Assert.AreEqual(64, result.Data.PointCount);
            Assert.IsTrue(result.Data.ScriptPath.EndsWith(".cs"));

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, result.Data.ScriptPath);
            Assert.IsTrue(File.Exists(fullPath));

            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }

        [Test]
        public void Create_Chain_ReturnsValidScript()
        {
            var result = PhysicsVerletCreateTool.Execute(new PhysicsVerletCreateParams
            {
                Type       = "chain",
                PointCount = 15,
                Name       = "TestChain"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("chain", result.Data.Type);
            Assert.AreEqual(15, result.Data.PointCount);
            Assert.IsTrue(result.Data.ScriptPath.EndsWith(".cs"));

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, result.Data.ScriptPath);
            Assert.IsTrue(File.Exists(fullPath));

            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }

        [Test]
        public void Create_InvalidType_ReturnsError()
        {
            var result = PhysicsVerletCreateTool.Execute(new PhysicsVerletCreateParams
            {
                Type = "jellyfish",
                Name = "Bad"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_PointCountLessThanTwo_IsClampedToTwo()
        {
            var result = PhysicsVerletCreateTool.Execute(new PhysicsVerletCreateParams
            {
                Type       = "rope",
                PointCount = 1,
                Name       = "ClampedRope"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.Data.PointCount);

            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }

        [Test]
        public void Create_ScriptPathIsUnderAssets()
        {
            var result = PhysicsVerletCreateTool.Execute(new PhysicsVerletCreateParams
            {
                Type       = "rope",
                PointCount = 5,
                Name       = "PathCheck"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.ScriptPath.StartsWith("Assets/Generated/Physics/"));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith("VerletSystem_PathCheck.cs"));

            _createdGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
        }
    }
}
