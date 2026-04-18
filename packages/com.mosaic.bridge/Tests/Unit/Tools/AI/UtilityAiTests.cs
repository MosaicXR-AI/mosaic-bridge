using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.AI;

namespace Mosaic.Bridge.Tests.Unit.Tools.AI
{
    [TestFixture]
    [Category("Unit")]
    public class UtilityAiTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("UAI_TestGO");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);

            // Clean up generated files
            var genPath = "Assets/Generated/AI/";
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullGenPath = System.IO.Path.Combine(projectRoot, genPath);
            if (System.IO.Directory.Exists(fullGenPath))
            {
                System.IO.Directory.Delete(fullGenPath, true);
                var metaPath = fullGenPath.TrimEnd('/') + ".meta";
                if (System.IO.File.Exists(metaPath))
                    System.IO.File.Delete(metaPath);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void Create_SimpleAgent_ReturnsSuccess()
        {
            var result = AiUtilityCreateTool.Execute(new AiUtilityCreateParams
            {
                AgentName = "TestGuard",
                Actions = new List<UtilityAction>
                {
                    new UtilityAction
                    {
                        Name = "Patrol",
                        MethodName = "DoPatrol",
                        Considerations = new List<Consideration>
                        {
                            new Consideration
                            {
                                InputAxis = "alertLevel",
                                Curve = "linear",
                                Slope = -1f,
                                Shift = 1f
                            }
                        }
                    },
                    new UtilityAction
                    {
                        Name = "Attack",
                        MethodName = "DoAttack",
                        Considerations = new List<Consideration>
                        {
                            new Consideration
                            {
                                InputAxis = "alertLevel",
                                Curve = "quadratic",
                                Exponent = 2f
                            }
                        }
                    }
                },
                Inputs = new List<InputDef>
                {
                    new InputDef { Name = "alertLevel", Type = "float", Source = "Threat detection system" }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.IsNotNull(result.Data.ScriptPath);
        }

        [Test]
        public void Create_ScriptPathIsValid()
        {
            var result = AiUtilityCreateTool.Execute(new AiUtilityCreateParams
            {
                AgentName = "TestPath",
                Actions = new List<UtilityAction>
                {
                    new UtilityAction
                    {
                        Name = "Idle",
                        Considerations = new List<Consideration>
                        {
                            new Consideration { InputAxis = "energy", Curve = "linear" }
                        }
                    }
                },
                Inputs = new List<InputDef>
                {
                    new InputDef { Name = "energy", Type = "float" }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.ScriptPath.StartsWith("Assets/"));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith(".cs"));

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = System.IO.Path.Combine(projectRoot, result.Data.ScriptPath);
            Assert.IsTrue(System.IO.File.Exists(fullPath), $"Script file does not exist at {fullPath}");
        }

        [Test]
        public void Create_ActionCountMatches()
        {
            var result = AiUtilityCreateTool.Execute(new AiUtilityCreateParams
            {
                AgentName = "TestCount",
                Actions = new List<UtilityAction>
                {
                    new UtilityAction { Name = "Eat" },
                    new UtilityAction { Name = "Sleep" },
                    new UtilityAction { Name = "Work" }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.ActionCount);
        }

        [Test]
        public void Create_MissingAgentName_ReturnsError()
        {
            var result = AiUtilityCreateTool.Execute(new AiUtilityCreateParams
            {
                AgentName = null,
                Actions = new List<UtilityAction>
                {
                    new UtilityAction { Name = "Idle" }
                }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_EmptyActions_ReturnsError()
        {
            var result = AiUtilityCreateTool.Execute(new AiUtilityCreateParams
            {
                AgentName = "TestEmpty",
                Actions = new List<UtilityAction>()
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_InvalidCurveType_ReturnsError()
        {
            var result = AiUtilityCreateTool.Execute(new AiUtilityCreateParams
            {
                AgentName = "TestBadCurve",
                Actions = new List<UtilityAction>
                {
                    new UtilityAction
                    {
                        Name = "Act",
                        Considerations = new List<Consideration>
                        {
                            new Consideration
                            {
                                InputAxis = "x",
                                Curve = "bezier"
                            }
                        }
                    }
                }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            Assert.IsTrue(result.Error.Contains("bezier"));
        }
    }
}
