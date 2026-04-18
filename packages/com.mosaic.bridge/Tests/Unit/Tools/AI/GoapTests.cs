using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.AI;

namespace Mosaic.Bridge.Tests.Unit.Tools.AI
{
    [TestFixture]
    [Category("Unit")]
    public class GoapTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GOAP_TestGO");
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
        public void Create_GoapAgent_ReturnsSuccessWithScriptPath()
        {
            var result = AiGoapCreateTool.Execute(new AiGoapCreateParams
            {
                AgentName = "TestGuard",
                WorldState = new[]
                {
                    new StateVar { Key = "hasWeapon", Type = "bool", Value = "false" },
                    new StateVar { Key = "enemyVisible", Type = "bool", Value = "false" }
                },
                Goals = new[]
                {
                    new GoalDef
                    {
                        Name = "KillEnemy",
                        Priority = 1f,
                        Conditions = new[] { new ConditionPair { Key = "enemyAlive", Value = "false" } }
                    }
                },
                Actions = new[]
                {
                    new ActionDef
                    {
                        Name = "PickUpWeapon",
                        Cost = 1f,
                        Preconditions = new[] { new ConditionPair { Key = "hasWeapon", Value = "false" } },
                        Effects = new[] { new ConditionPair { Key = "hasWeapon", Value = "true" } },
                        MethodName = "PickUpWeapon"
                    },
                    new ActionDef
                    {
                        Name = "Attack",
                        Cost = 2f,
                        Preconditions = new[] { new ConditionPair { Key = "hasWeapon", Value = "true" } },
                        Effects = new[] { new ConditionPair { Key = "enemyAlive", Value = "false" } },
                        MethodName = "Attack"
                    }
                }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.IsNotNull(result.Data.ScriptPath);
            Assert.IsTrue(result.Data.ScriptPath.StartsWith("Assets/"));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith(".cs"));
            Assert.AreEqual(1, result.Data.GoalCount);
            Assert.AreEqual(2, result.Data.ActionCount);

            // Verify file exists on disk
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = System.IO.Path.Combine(projectRoot, result.Data.ScriptPath);
            Assert.IsTrue(System.IO.File.Exists(fullPath), $"Script file does not exist at {fullPath}");
        }

        [Test]
        public void Plan_ReturnsValidActionSequence()
        {
            // First create a GOAP agent so there's a generated script to analyze
            var createResult = AiGoapCreateTool.Execute(new AiGoapCreateParams
            {
                AgentName = "TestPlanner",
                WorldState = new[]
                {
                    new StateVar { Key = "atBase", Type = "bool", Value = "true" },
                    new StateVar { Key = "hasSupplies", Type = "bool", Value = "false" }
                },
                Goals = new[]
                {
                    new GoalDef
                    {
                        Name = "GetSupplies",
                        Priority = 1f,
                        Conditions = new[] { new ConditionPair { Key = "hasSupplies", Value = "true" } }
                    }
                },
                Actions = new[]
                {
                    new ActionDef
                    {
                        Name = "GoToSupplyDepot",
                        Cost = 1f,
                        Preconditions = new[] { new ConditionPair { Key = "atBase", Value = "true" } },
                        Effects = new[] { new ConditionPair { Key = "atDepot", Value = "true" } },
                        MethodName = "GoToSupplyDepot"
                    },
                    new ActionDef
                    {
                        Name = "PickUpSupplies",
                        Cost = 1f,
                        Preconditions = new[] { new ConditionPair { Key = "atDepot", Value = "true" } },
                        Effects = new[] { new ConditionPair { Key = "hasSupplies", Value = "true" } },
                        MethodName = "PickUpSupplies"
                    }
                }
            });
            Assert.IsTrue(createResult.Success, createResult.Error);

            // Run the plan tool (will use static script analysis since script isn't compiled)
            var planResult = AiGoapPlanTool.Execute(new AiGoapPlanParams
            {
                GameObjectName = "GOAP_TestGO",
                MaxPlanDepth = 10
            });

            Assert.IsTrue(planResult.Success, planResult.Error);
            Assert.IsNotNull(planResult.Data);
            Assert.IsNotNull(planResult.Data.Plan);
            Assert.Greater(planResult.Data.Plan.Length, 0, "Plan should contain at least one step");
        }

        [Test]
        public void Validate_IdentifiesUnreachableGoals()
        {
            // Test the validation logic directly — no generated script or compiled component needed
            var worldState = new Dictionary<string, object>
            {
                { "isAlive", (object)true }
            };

            var goals = new List<(string name, Dictionary<string, object> conditions)>
            {
                ("ReachableGoal", new Dictionary<string, object> { { "hasTool", (object)true } }),
                ("UnreachableGoal", new Dictionary<string, object> { { "canFly", (object)true } })
            };

            var actions = new List<(string name, Dictionary<string, object> preconditions, Dictionary<string, object> effects)>
            {
                ("GrabTool",
                    new Dictionary<string, object> { { "isAlive", (object)true } },
                    new Dictionary<string, object> { { "hasTool", (object)true } })
            };

            var result = AiGoapValidateTool.PerformValidation(worldState, goals, actions);

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.IsFalse(result.Data.IsValid, "Should be invalid due to unreachable goal");
            Assert.IsTrue(result.Data.AchievableGoals.Length > 0, "ReachableGoal should be achievable");
            Assert.Contains("ReachableGoal", result.Data.AchievableGoals);
            Assert.IsTrue(result.Data.UnachievableGoals.Length > 0, "UnreachableGoal should be listed");

            bool foundUnreachable = false;
            foreach (var ug in result.Data.UnachievableGoals)
            {
                if (ug.Goal == "UnreachableGoal")
                {
                    foundUnreachable = true;
                    Assert.IsNotNull(ug.MissingEffect);
                }
            }
            Assert.IsTrue(foundUnreachable, "UnreachableGoal should be in UnachievableGoals list");
        }

        [Test]
        public void Create_MissingAgentName_ReturnsError()
        {
            var result = AiGoapCreateTool.Execute(new AiGoapCreateParams
            {
                AgentName = null,
                Goals = new[]
                {
                    new GoalDef
                    {
                        Name = "SomeGoal",
                        Priority = 1f,
                        Conditions = new[] { new ConditionPair { Key = "done", Value = "true" } }
                    }
                },
                Actions = new[]
                {
                    new ActionDef
                    {
                        Name = "DoSomething",
                        Cost = 1f,
                        MethodName = "DoSomething"
                    }
                }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_EmptyActionsList_ReturnsError()
        {
            var result = AiGoapCreateTool.Execute(new AiGoapCreateParams
            {
                AgentName = "TestEmpty",
                Goals = new[]
                {
                    new GoalDef
                    {
                        Name = "SomeGoal",
                        Priority = 1f,
                        Conditions = new[] { new ConditionPair { Key = "done", Value = "true" } }
                    }
                },
                Actions = new ActionDef[0]
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
