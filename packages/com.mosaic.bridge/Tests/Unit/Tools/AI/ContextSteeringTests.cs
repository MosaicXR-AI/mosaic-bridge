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
    public class ContextSteeringTests
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
        public void ContextSteering_ValidGO_ReturnsSuccessWithScriptPath()
        {
            _testGo = new GameObject("CtxSteerTest_Valid");

            var result = AiContextSteeringTool.Execute(new AiContextSteeringParams
            {
                GameObjectName = "CtxSteerTest_Valid",
                InterestSources = new List<InterestSource>
                {
                    new InterestSource { Type = "target", Value = "CtxSteerTest_Valid" }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("CtxSteerTest_Valid", result.Data.GameObjectName);
            Assert.IsNotNull(result.Data.ScriptPath);
            Assert.IsTrue(result.Data.ScriptPath.EndsWith(".cs"));

            string fullPath = Path.Combine(Application.dataPath, "..", result.Data.ScriptPath);
            Assert.IsTrue(File.Exists(fullPath), $"Generated script should exist at {fullPath}");

            if (result.Data != null)
                _generatedFiles.Add(result.Data.ScriptPath);
        }

        [Test]
        public void ContextSteering_ResolutionParameterRespected()
        {
            _testGo = new GameObject("CtxSteerTest_Res");

            var result = AiContextSteeringTool.Execute(new AiContextSteeringParams
            {
                GameObjectName = "CtxSteerTest_Res",
                Resolution = 32,
                InterestSources = new List<InterestSource>
                {
                    new InterestSource { Type = "target", Value = "CtxSteerTest_Res" }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(32, result.Data.Resolution);

            // Verify the generated script contains the resolution value
            string fullPath = Path.Combine(Application.dataPath, "..", result.Data.ScriptPath);
            string content = File.ReadAllText(fullPath);
            Assert.IsTrue(content.Contains("public int resolution = 32;"),
                "Generated script should contain resolution = 32");

            _generatedFiles.Add(result.Data.ScriptPath);
        }

        [Test]
        public void ContextSteering_MissingGO_ReturnsError()
        {
            var result = AiContextSteeringTool.Execute(new AiContextSteeringParams
            {
                GameObjectName = "CtxSteerTest_DoesNotExist_99999",
                InterestSources = new List<InterestSource>
                {
                    new InterestSource { Type = "target", Value = "SomeTarget" }
                }
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("not found"));
        }

        [Test]
        public void ContextSteering_EmptyInterestSources_ReturnsError()
        {
            _testGo = new GameObject("CtxSteerTest_NoInterest");

            var result = AiContextSteeringTool.Execute(new AiContextSteeringParams
            {
                GameObjectName = "CtxSteerTest_NoInterest",
                InterestSources = new List<InterestSource>()
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("At least one interest source"));
        }

        [Test]
        public void ContextSteering_InvalidSourceType_ReturnsError()
        {
            _testGo = new GameObject("CtxSteerTest_InvalidType");

            var result = AiContextSteeringTool.Execute(new AiContextSteeringParams
            {
                GameObjectName = "CtxSteerTest_InvalidType",
                InterestSources = new List<InterestSource>
                {
                    new InterestSource { Type = "nonexistent_type", Value = "SomeValue" }
                }
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Unknown interest source type"));
        }

        [Test]
        public void ContextSteering_MultipleSources_CreatesCombinedAgent()
        {
            _testGo = new GameObject("CtxSteerTest_Multi");

            var result = AiContextSteeringTool.Execute(new AiContextSteeringParams
            {
                GameObjectName = "CtxSteerTest_Multi",
                MaxSpeed = 10f,
                Resolution = 24,
                InterestSources = new List<InterestSource>
                {
                    new InterestSource { Type = "target", Value = "CtxSteerTest_Multi", Weight = 1.5f },
                    new InterestSource { Type = "direction", Value = "1,0,0", Weight = 0.5f }
                },
                DangerSources = new List<DangerSource>
                {
                    new DangerSource { Type = "obstacle", Value = "SomeObstacle", Weight = 2.0f, Radius = 8.0f },
                    new DangerSource { Type = "agent", Value = "SomeAgent", Weight = 1.0f }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.Data.InterestSourceCount);
            Assert.AreEqual(2, result.Data.DangerSourceCount);
            Assert.AreEqual(24, result.Data.Resolution);

            // Verify generated script contains expected fields
            string fullPath = Path.Combine(Application.dataPath, "..", result.Data.ScriptPath);
            string content = File.ReadAllText(fullPath);
            Assert.IsTrue(content.Contains("interestTarget_0"), "Should have target field");
            Assert.IsTrue(content.Contains("interestDirection_0"), "Should have direction field");
            Assert.IsTrue(content.Contains("dangerSource_0"), "Should have first danger field");
            Assert.IsTrue(content.Contains("dangerSource_1"), "Should have second danger field");
            Assert.IsTrue(content.Contains("maxSpeed = 10.0f"), "Should have correct max speed");

            _generatedFiles.Add(result.Data.ScriptPath);
        }
    }
}
