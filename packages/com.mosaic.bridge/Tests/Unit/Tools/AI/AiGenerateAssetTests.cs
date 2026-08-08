using System.Linq;
using NUnit.Framework;
using Mosaic.Bridge.Tools.AI;

namespace Mosaic.Bridge.Tests.Unit.Tools.AI
{
    /// <summary>
    /// Covers ai/generate-asset. The tool prepares a request and never generates, so these
    /// tests assert the request is well-formed, that knowledge-base grounding is applied and
    /// auditable, and that the mode reflects whether Unity AI is actually present.
    /// </summary>
    [TestFixture]
    public class AiGenerateAssetTests
    {
        private static AiGenerateAssetParams P(string prompt = "a plain surface",
                                               string type = "Texture",
                                               string name = "test-asset") =>
            new AiGenerateAssetParams { Prompt = prompt, AssetType = type, Name = name };

        // ── validation ───────────────────────────────────────────────────────

        [Test]
        public void NullParams_Fails()
        {
            Assert.IsFalse(AiGenerateAssetTool.Execute(null).Success);
        }

        [Test]
        public void MissingPrompt_Fails()
        {
            var p = P(); p.Prompt = "  ";
            var r = AiGenerateAssetTool.Execute(p);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("Prompt", r.Error);
        }

        [Test]
        public void MissingName_Fails()
        {
            var p = P(); p.Name = null;
            Assert.IsFalse(AiGenerateAssetTool.Execute(p).Success);
        }

        [Test]
        public void UnknownAssetType_FailsAndListsValidOnes()
        {
            var r = AiGenerateAssetTool.Execute(P(type: "Hologram"));
            Assert.IsFalse(r.Success);
            StringAssert.Contains("Material", r.Error);
        }

        [Test]
        public void DestinationOutsideAssets_Fails()
        {
            var p = P();
            p.DestinationFolder = "/etc";
            Assert.IsFalse(AiGenerateAssetTool.Execute(p).Success);
        }

        // ── request shape ────────────────────────────────────────────────────

        [Test]
        public void MapsEachAssetTypeToACommand()
        {
            foreach (var (type, command) in new[]
                     {
                         ("Material", "GenerateMaterial"), ("Texture", "GenerateImage"),
                         ("Sprite", "GenerateSprite"), ("Sound", "GenerateSound"),
                     })
            {
                var r = AiGenerateAssetTool.Execute(P(type: type));
                Assert.IsTrue(r.Success, type);
                Assert.AreEqual(command, r.Data.Command, type);
            }
        }

        [Test]
        public void BuildsSavePathUnderDestination()
        {
            var p = P(name: "floor-oak", type: "Material");
            p.DestinationFolder = "Assets/Art/Materials";
            var r = AiGenerateAssetTool.Execute(p);
            Assert.AreEqual("Assets/Art/Materials/floor-oak.mat", r.Data.SavePath);
        }

        [Test]
        public void DefaultsDestinationToAssetsGenerated()
        {
            var r = AiGenerateAssetTool.Execute(P(name: "thing"));
            StringAssert.StartsWith("Assets/Generated/", r.Data.SavePath);
        }

        [Test]
        public void SanitizesUnsafeNames()
        {
            var r = AiGenerateAssetTool.Execute(P(name: "my asset/name!!"));
            Assert.IsTrue(r.Success);
            Assert.IsFalse(r.Data.SavePath.Contains(" "), r.Data.SavePath);
            Assert.IsFalse(r.Data.SavePath.Contains("!"), r.Data.SavePath);
            Assert.IsFalse(r.Data.SavePath.Contains("//"), r.Data.SavePath);
        }

        [Test]
        public void ModeAndCreditFlagAgreeWithAvailability()
        {
            var d = AiGenerateAssetTool.Execute(P()).Data;
            Assert.AreEqual(d.UnityAiAvailable ? "unity-mcp" : "handoff", d.Mode);
            // Credits are only spent when generation can actually run.
            Assert.AreEqual(d.UnityAiAvailable, d.ConsumesCredits);
            Assert.IsNotNull(d.NextStep);
        }

        // ── knowledge-base grounding (the part Unity's generator can't do) ───

        [Test]
        public void GroundsKnownMaterialWithMeasuredValues()
        {
            var r = AiGenerateAssetTool.Execute(P("seamless wood oak floor planks", "Material", "floor-oak"));
            Assert.IsTrue(r.Success);
            Assert.IsNotEmpty(r.Data.KnowledgeApplied,
                "wood_oak is in the PBR knowledge base and should have been folded into the prompt");
            StringAssert.Contains("albedo", r.Data.Prompt.ToLowerInvariant());
        }

        [Test]
        public void RecordsTheSourceOfEveryInjectedNumber()
        {
            var r = AiGenerateAssetTool.Execute(P("wood oak planks", "Material", "oak"));
            foreach (var entry in r.Data.KnowledgeApplied)
                StringAssert.Contains("—", entry, "each grounded value must carry its source");
        }

        [Test]
        public void LeavesUnknownMaterialsAlone()
        {
            var r = AiGenerateAssetTool.Execute(P("an alien crystal from nowhere", "Material", "x"));
            Assert.IsEmpty(r.Data.KnowledgeApplied);
            Assert.AreEqual("an alien crystal from nowhere", r.Data.Prompt);
        }

        [Test]
        public void GroundingCanBeDisabled()
        {
            var p = P("wood oak planks", "Material", "oak");
            p.GroundInKnowledgeBase = false;
            var r = AiGenerateAssetTool.Execute(p);
            Assert.IsEmpty(r.Data.KnowledgeApplied);
            Assert.AreEqual("wood oak planks", r.Data.Prompt);
        }

        [Test]
        public void PrefersTheMoreSpecificMaterial()
        {
            // "wood oak" must not be satisfied by wood_pine just because "wood" matches.
            var r = AiGenerateAssetTool.Execute(P("wood oak surface", "Material", "oak"));
            Assert.IsTrue(r.Data.KnowledgeApplied.All(e => !e.ToLowerInvariant().Contains("pine")),
                string.Join(" | ", r.Data.KnowledgeApplied));
        }

        [Test]
        public void IsReadOnly_DoesNotTouchTheProject()
        {
            var p = P(name: "never-written");
            p.DestinationFolder = "Assets/DefinitelyDoesNotExist";
            var r = AiGenerateAssetTool.Execute(p);
            Assert.IsTrue(r.Success);
            Assert.IsFalse(UnityEditor.AssetDatabase.IsValidFolder("Assets/DefinitelyDoesNotExist"),
                "preparing a request must not create folders or assets");
        }
    }
}
