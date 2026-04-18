using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.AI;

namespace Mosaic.Bridge.Tests.Unit.Tools.AI
{
    [TestFixture]
    [Category("Unit")]
    public class BehaviorTreeTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("BT_TestGO");
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
        public void Create_SimpleSelectorTree_ReturnsSuccess()
        {
            var result = AiBehaviorTreeCreateTool.Execute(new AiBehaviorTreeCreateParams
            {
                Name = "TestPatrol",
                RootNode = new TreeNodeDef
                {
                    Type = "selector",
                    Name = "Root",
                    Children = new[]
                    {
                        new TreeNodeDef
                        {
                            Type = "action",
                            Name = "Patrol",
                            Action = "Patrol"
                        },
                        new TreeNodeDef
                        {
                            Type = "action",
                            Name = "Idle",
                            Action = "Idle"
                        }
                    }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.IsNotNull(result.Data.ScriptPath);
        }

        [Test]
        public void Create_GeneratedScriptPathIsValid()
        {
            var result = AiBehaviorTreeCreateTool.Execute(new AiBehaviorTreeCreateParams
            {
                Name = "TestNav",
                RootNode = new TreeNodeDef
                {
                    Type = "sequence",
                    Name = "Root",
                    Children = new[]
                    {
                        new TreeNodeDef { Type = "action", Name = "Move", Action = "Move" }
                    }
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
        public void Create_NodeCountMatchesDefinition()
        {
            // 1 selector + 2 actions = 3 nodes
            var result = AiBehaviorTreeCreateTool.Execute(new AiBehaviorTreeCreateParams
            {
                Name = "TestCount",
                RootNode = new TreeNodeDef
                {
                    Type = "selector",
                    Name = "Root",
                    Children = new[]
                    {
                        new TreeNodeDef { Type = "action", Name = "A", Action = "ActionA" },
                        new TreeNodeDef { Type = "action", Name = "B", Action = "ActionB" }
                    }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.NodeCount);
        }

        [Test]
        public void Create_BlackboardVarsIncluded()
        {
            var result = AiBehaviorTreeCreateTool.Execute(new AiBehaviorTreeCreateParams
            {
                Name = "TestBB",
                RootNode = new TreeNodeDef
                {
                    Type = "selector",
                    Name = "Root",
                    Children = new[]
                    {
                        new TreeNodeDef { Type = "action", Name = "Act", Action = "DoStuff" }
                    }
                },
                Blackboard = new[]
                {
                    new BlackboardVar { Key = "health",    Type = "float",  DefaultValue = "100" },
                    new BlackboardVar { Key = "isAlerted", Type = "bool",   DefaultValue = "false" },
                    new BlackboardVar { Key = "targetPos", Type = "Vector3" }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.BlackboardVarCount);

            // Verify the generated script contains blackboard fields
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var content = System.IO.File.ReadAllText(
                System.IO.Path.Combine(projectRoot, result.Data.ScriptPath));
            Assert.IsTrue(content.Contains("public float health"), "Missing health field");
            Assert.IsTrue(content.Contains("public bool isAlerted"), "Missing isAlerted field");
            Assert.IsTrue(content.Contains("public Vector3 targetPos"), "Missing targetPos field");
        }

        [Test]
        public void Create_MissingName_ReturnsError()
        {
            var result = AiBehaviorTreeCreateTool.Execute(new AiBehaviorTreeCreateParams
            {
                Name = null,
                RootNode = new TreeNodeDef
                {
                    Type = "selector",
                    Name = "Root",
                    Children = new[]
                    {
                        new TreeNodeDef { Type = "action", Name = "A", Action = "Act" }
                    }
                }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_AttachToNonExistentGO_ReturnsError()
        {
            var result = AiBehaviorTreeCreateTool.Execute(new AiBehaviorTreeCreateParams
            {
                Name = "TestAttach",
                RootNode = new TreeNodeDef
                {
                    Type = "selector",
                    Name = "Root",
                    Children = new[]
                    {
                        new TreeNodeDef { Type = "action", Name = "A", Action = "Act" }
                    }
                },
                AttachTo = "NonExistentGameObject_12345"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}
