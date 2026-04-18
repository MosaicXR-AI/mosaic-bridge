using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.AI;

namespace Mosaic.Bridge.Tests.Unit.Tools.AI
{
    [TestFixture]
    [Category("AI")]
    public class SteeringTests
    {
        private GameObject _testGo;
        private readonly List<string> _generatedFiles = new List<string>();

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null)
            {
                Object.DestroyImmediate(_testGo);
                _testGo = null;
            }

            // Clean up generated scripts
            foreach (var path in _generatedFiles)
            {
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    string metaPath = fullPath + ".meta";
                    if (File.Exists(metaPath))
                        File.Delete(metaPath);
                }
            }
            _generatedFiles.Clear();
        }

        [Test]
        public void SteeringAdd_ValidGO_ReturnsSuccess()
        {
            _testGo = new GameObject("SteeringTest_Valid");

            var result = AiSteeringAddTool.Execute(new AiSteeringAddParams
            {
                GameObjectName = "SteeringTest_Valid",
                Behaviors = new List<SteeringBehavior>
                {
                    new SteeringBehavior { Type = "seek" }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("SteeringTest_Valid", result.Data.GameObjectName);
            Assert.AreEqual(1, result.Data.BehaviorCount);

            if (result.Data != null)
                _generatedFiles.Add(result.Data.ScriptPath);
        }

        [Test]
        public void SteeringAdd_GeneratedScriptPathIsValid()
        {
            _testGo = new GameObject("SteeringTest_Path");

            var result = AiSteeringAddTool.Execute(new AiSteeringAddParams
            {
                GameObjectName = "SteeringTest_Path",
                Behaviors = new List<SteeringBehavior>
                {
                    new SteeringBehavior { Type = "wander" }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.ScriptPath);
            Assert.IsTrue(result.Data.ScriptPath.StartsWith("Assets/Generated/AI/"));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith(".cs"));

            string fullPath = Path.Combine(Application.dataPath, "..", result.Data.ScriptPath);
            Assert.IsTrue(File.Exists(fullPath), $"Generated script should exist at {fullPath}");

            _generatedFiles.Add(result.Data.ScriptPath);
        }

        [Test]
        public void SteeringAdd_InvalidBehaviorType_ReturnsError()
        {
            _testGo = new GameObject("SteeringTest_InvalidType");

            var result = AiSteeringAddTool.Execute(new AiSteeringAddParams
            {
                GameObjectName = "SteeringTest_InvalidType",
                Behaviors = new List<SteeringBehavior>
                {
                    new SteeringBehavior { Type = "nonexistent_behavior" }
                }
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Unknown behavior type"));
        }

        [Test]
        public void SteeringAdd_MissingGO_ReturnsError()
        {
            var result = AiSteeringAddTool.Execute(new AiSteeringAddParams
            {
                GameObjectName = "SteeringTest_DoesNotExist_99999",
                Behaviors = new List<SteeringBehavior>
                {
                    new SteeringBehavior { Type = "seek" }
                }
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("not found"));
        }

        [Test]
        public void SteeringAdd_MultipleBehaviors_CreatesCombinedAgent()
        {
            _testGo = new GameObject("SteeringTest_Multi");

            var result = AiSteeringAddTool.Execute(new AiSteeringAddParams
            {
                GameObjectName = "SteeringTest_Multi",
                MaxSpeed = 8f,
                MaxForce = 15f,
                Behaviors = new List<SteeringBehavior>
                {
                    new SteeringBehavior { Type = "seek", Weight = 1.5f },
                    new SteeringBehavior { Type = "obstacle_avoidance", Weight = 2.0f },
                    new SteeringBehavior { Type = "separation", Weight = 1.0f }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.BehaviorCount);
            Assert.AreEqual(8f, result.Data.MaxSpeed, 0.001f);
            CollectionAssert.AreEquivalent(
                new[] { "seek", "obstacle_avoidance", "separation" },
                result.Data.Behaviors);

            _generatedFiles.Add(result.Data.ScriptPath);
        }

        [Test]
        public void SteeringAdd_NoBehaviors_ReturnsError()
        {
            _testGo = new GameObject("SteeringTest_NoBehaviors");

            var result = AiSteeringAddTool.Execute(new AiSteeringAddParams
            {
                GameObjectName = "SteeringTest_NoBehaviors",
                Behaviors = new List<SteeringBehavior>()
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("At least one behavior"));
        }

        [Test]
        public void SteeringAdd_NullGameObjectName_ReturnsError()
        {
            var result = AiSteeringAddTool.Execute(new AiSteeringAddParams
            {
                GameObjectName = null,
                Behaviors = new List<SteeringBehavior>
                {
                    new SteeringBehavior { Type = "seek" }
                }
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("GameObjectName is required"));
        }
    }
}
