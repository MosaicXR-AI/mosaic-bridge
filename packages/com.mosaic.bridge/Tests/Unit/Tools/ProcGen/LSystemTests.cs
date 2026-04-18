using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Tools.ProcGen;

namespace Mosaic.Bridge.Tests.Unit.Tools.ProcGen
{
    [TestFixture]
    [Category("ProcGen")]
    public class LSystemTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
            _created.Clear();
        }

        private ToolResult<ProcGenLSystemResult> ExecuteAndTrack(ProcGenLSystemParams p)
        {
            var result = ProcGenLSystemTool.Execute(p);
            if (result.Success && result.Data.InstanceId != 0)
            {
                var go = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
                if (go != null) _created.Add(go);
            }
            return result;
        }

        // ── Preset generates valid mesh ────────────────────────────────────

        [Test]
        public void Execute_PresetTree_GeneratesValidMeshWithVertices()
        {
            var result = ExecuteAndTrack(new ProcGenLSystemParams
            {
                Preset     = "tree",
                Iterations = 2,
                Seed       = 42
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.GameObjectName);
            Assert.Greater(result.Data.VertexCount, 0, "Tree should produce vertices");
            Assert.Greater(result.Data.BranchCount, 0, "Tree should produce branches");
            Assert.AreEqual("tree", result.Data.Preset);
        }

        // ── String expansion correctness ───────────────────────────────────

        [Test]
        public void ExpandString_KnownRules_ProducesCorrectOutput()
        {
            // Simple rule: A -> AB, B -> A
            // Iteration 0: A
            // Iteration 1: AB
            // Iteration 2: ABA
            // Iteration 3: ABAAB
            var rules = new List<LSystemRule>
            {
                new LSystemRule { Symbol = "A", Replacement = "AB" },
                new LSystemRule { Symbol = "B", Replacement = "A" }
            };

            var rng = new System.Random(0);
            string result = ProcGenLSystemTool.ExpandString("A", rules, 3, rng);
            Assert.AreEqual("ABAAB", result);
        }

        [Test]
        public void ExpandString_OneIteration_SingleRule()
        {
            var rules = new List<LSystemRule>
            {
                new LSystemRule { Symbol = "F", Replacement = "F+F" }
            };

            var rng = new System.Random(0);
            string result = ProcGenLSystemTool.ExpandString("F", rules, 1, rng);
            Assert.AreEqual("F+F", result);
        }

        // ── Deterministic with seed ────────────────────────────────────────

        [Test]
        public void Execute_SameSeed_ProducesDeterministicOutput()
        {
            var p = new ProcGenLSystemParams
            {
                Preset     = "fern",
                Iterations = 3,
                Seed       = 12345,
                GenerateMesh = false
            };

            var r1 = ProcGenLSystemTool.Execute(p);
            var r2 = ProcGenLSystemTool.Execute(p);

            Assert.IsTrue(r1.Success);
            Assert.IsTrue(r2.Success);
            Assert.AreEqual(r1.Data.GeneratedString, r2.Data.GeneratedString);
            Assert.AreEqual(r1.Data.StringLength, r2.Data.StringLength);
            Assert.AreEqual(r1.Data.BranchCount, r2.Data.BranchCount);
        }

        // ── Stochastic rules produce variation ─────────────────────────────

        [Test]
        public void Execute_StochasticRules_ProduceVariationWithoutSeed()
        {
            var rules = new List<LSystemRule>
            {
                new LSystemRule { Symbol = "F", Replacement = "F[+F]F", Probability = 0.5f },
                new LSystemRule { Symbol = "F", Replacement = "F[-F]F", Probability = 0.5f }
            };

            // Run multiple times with different seeds to verify variation is possible
            var results = new HashSet<string>();
            for (int seed = 0; seed < 20; seed++)
            {
                var result = ProcGenLSystemTool.Execute(new ProcGenLSystemParams
                {
                    Axiom        = "F",
                    Rules        = rules,
                    Iterations   = 2,
                    Seed         = seed,
                    GenerateMesh = false
                });

                Assert.IsTrue(result.Success, result.Error);
                results.Add(result.Data.GeneratedString);
            }

            Assert.Greater(results.Count, 1,
                "Stochastic rules with different seeds should produce at least some variation");
        }

        // ── Invalid iterations clamped ─────────────────────────────────────

        [Test]
        public void Execute_IterationsZero_ClampedToOne()
        {
            var result = ExecuteAndTrack(new ProcGenLSystemParams
            {
                Axiom      = "F",
                Rules      = new List<LSystemRule>
                {
                    new LSystemRule { Symbol = "F", Replacement = "FF" }
                },
                Iterations = 0,
                Seed       = 1
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Iterations, "Iterations below 1 should be clamped to 1");
        }

        [Test]
        public void Execute_IterationsAboveMax_ClampedToEight()
        {
            var result = ProcGenLSystemTool.Execute(new ProcGenLSystemParams
            {
                Axiom      = "F",
                Rules      = new List<LSystemRule>
                {
                    new LSystemRule { Symbol = "F", Replacement = "FF" }
                },
                Iterations   = 99,
                Seed         = 1,
                GenerateMesh = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(8, result.Data.Iterations, "Iterations above 8 should be clamped to 8");
        }

        // ── GenerateMesh=false returns string only, no GO ──────────────────

        [Test]
        public void Execute_GenerateMeshFalse_ReturnsStringOnly()
        {
            var result = ProcGenLSystemTool.Execute(new ProcGenLSystemParams
            {
                Preset       = "bush",
                Iterations   = 2,
                Seed         = 7,
                GenerateMesh = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNull(result.Data.GameObjectName, "Should not create a GameObject");
            Assert.AreEqual(0, result.Data.InstanceId, "InstanceId should be 0");
            Assert.AreEqual(0, result.Data.VertexCount, "VertexCount should be 0");
            Assert.Greater(result.Data.StringLength, 0, "Should still produce a string");
            Assert.Greater(result.Data.BranchCount, 0, "Should still count branches");
        }

        // ── Invalid preset ─────────────────────────────────────────────────

        [Test]
        public void Execute_InvalidPreset_ReturnsError()
        {
            var result = ProcGenLSystemTool.Execute(new ProcGenLSystemParams
            {
                Preset = "invalid_preset"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            Assert.IsTrue(result.Error.Contains("invalid_preset"));
        }

        // ── Invalid rule symbol ────────────────────────────────────────────

        [Test]
        public void Execute_InvalidRuleSymbol_ReturnsError()
        {
            var result = ProcGenLSystemTool.Execute(new ProcGenLSystemParams
            {
                Axiom = "F",
                Rules = new List<LSystemRule>
                {
                    new LSystemRule { Symbol = "AB", Replacement = "F+F" }
                }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── All presets produce output ─────────────────────────────────────

        [Test]
        [TestCase("tree")]
        [TestCase("fern")]
        [TestCase("bush")]
        [TestCase("coral")]
        [TestCase("vine")]
        public void Execute_AllPresets_ProduceValidOutput(string preset)
        {
            var result = ProcGenLSystemTool.Execute(new ProcGenLSystemParams
            {
                Preset       = preset,
                Iterations   = 2,
                Seed         = 42,
                GenerateMesh = false
            });

            Assert.IsTrue(result.Success, $"Preset '{preset}' failed: {result.Error}");
            Assert.AreEqual(preset, result.Data.Preset);
            Assert.Greater(result.Data.StringLength, 0);
        }

        // ── String truncation ──────────────────────────────────────────────

        [Test]
        public void Execute_LongString_TruncatedInResult()
        {
            var result = ProcGenLSystemTool.Execute(new ProcGenLSystemParams
            {
                Preset       = "bush",
                Iterations   = 6,
                Seed         = 1,
                GenerateMesh = false
            });

            Assert.IsTrue(result.Success, result.Error);
            // Bush at 6 iterations should produce a very long string
            if (result.Data.StringLength > 1000)
            {
                Assert.IsTrue(result.Data.GeneratedString.EndsWith("..."),
                    "Long strings should be truncated with '...'");
                Assert.AreEqual(1003, result.Data.GeneratedString.Length,
                    "Truncated string should be 1000 chars + '...'");
            }
        }
    }
}
