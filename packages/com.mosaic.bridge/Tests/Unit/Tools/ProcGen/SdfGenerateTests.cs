using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.ProcGen;

namespace Mosaic.Bridge.Tests.Unit.Tools.ProcGen
{
    [TestFixture]
    [Category("ProcGen")]
    public class SdfGenerateTests
    {
        private const string TestOutputDir = "Assets/Generated/SDF/Tests/";
        private string _fullOutputDir;

        [SetUp]
        public void SetUp()
        {
            _fullOutputDir = Path.Combine(Application.dataPath, "..", TestOutputDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_fullOutputDir))
            {
                Directory.Delete(_fullOutputDir, true);
                string metaFile = _fullOutputDir.TrimEnd('/', '\\') + ".meta";
                if (File.Exists(metaFile))
                    File.Delete(metaFile);
                AssetDatabase.Refresh();
            }
        }

        // ── Primitive sphere produces valid Texture3D ────────────────────

        [Test]
        public void PrimitiveSphere_ProducesValidTexture3D()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Source        = "primitive",
                PrimitiveType = "sphere",
                PrimitiveSize = new[] { 1f, 1f, 1f },
                Resolution    = 16,
                SavePath      = TestOutputDir,
                OutputName    = "sphere_test"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(16, result.Data.Resolution);
            Assert.AreEqual("primitive", result.Data.Source);
            Assert.IsNotNull(result.Data.AssetPath);

            var tex = AssetDatabase.LoadAssetAtPath<Texture3D>(result.Data.AssetPath);
            Assert.IsNotNull(tex, "Expected Texture3D asset at " + result.Data.AssetPath);
            Assert.AreEqual(16, tex.width);
            Assert.AreEqual(16, tex.height);
            Assert.AreEqual(16, tex.depth);

            // Sphere radius 1 centered at origin, bounds [-5,5] — centre voxel should be interior (negative)
            Assert.Less(result.Data.MinDistance, 0f, "Sphere interior should yield negative SDF values");
            Assert.Greater(result.Data.MaxDistance, 0f, "Sphere exterior should yield positive SDF values");
        }

        // ── Primitive box produces valid SDF ─────────────────────────────

        [Test]
        public void PrimitiveBox_ProducesValidSdf()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Source        = "primitive",
                PrimitiveType = "box",
                PrimitiveSize = new[] { 2f, 2f, 2f },
                Resolution    = 8,
                SavePath      = TestOutputDir,
                OutputName    = "box_test"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.Less(result.Data.MinDistance, 0f);
            Assert.Greater(result.Data.MaxDistance, 0f);

            var tex = AssetDatabase.LoadAssetAtPath<Texture3D>(result.Data.AssetPath);
            Assert.IsNotNull(tex);
            Assert.AreEqual(TextureFormat.RFloat, tex.format);
        }

        // ── Resolution clamped [4, 128] ──────────────────────────────────

        [Test]
        public void Resolution_ClampedBelowMin()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Source        = "primitive",
                PrimitiveType = "sphere",
                PrimitiveSize = new[] { 1f, 1f, 1f },
                Resolution    = 2,
                SavePath      = TestOutputDir,
                OutputName    = "clamp_low"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(4, result.Data.Resolution);
        }

        [Test]
        public void Resolution_ClampedAboveMax()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Source        = "primitive",
                PrimitiveType = "sphere",
                PrimitiveSize = new[] { 1f, 1f, 1f },
                Resolution    = 1024,
                SavePath      = TestOutputDir,
                OutputName    = "clamp_high"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(128, result.Data.Resolution);
        }

        // ── Invalid source returns error ─────────────────────────────────

        [Test]
        public void InvalidSource_ReturnsError()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Source     = "invalid_source",
                Resolution = 8,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void MissingSource_ReturnsError()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Resolution = 8,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Union operation between two primitives ───────────────────────

        [Test]
        public void UnionOperation_BetweenTwoPrimitives_Succeeds()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Source               = "primitive",
                PrimitiveType        = "sphere",
                PrimitiveSize        = new[] { 1f, 1f, 1f },
                Resolution           = 8,
                Operation            = "union",
                OperandPrimitive     = "box",
                OperandPrimitiveSize = new[] { 2f, 2f, 2f },
                SavePath             = TestOutputDir,
                OutputName           = "union_test"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("union", result.Data.Operation);
            Assert.Less(result.Data.MinDistance, 0f);

            var tex = AssetDatabase.LoadAssetAtPath<Texture3D>(result.Data.AssetPath);
            Assert.IsNotNull(tex);
        }

        // ── Missing operand for operation returns error ──────────────────

        [Test]
        public void Operation_WithoutOperand_ReturnsError()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Source        = "primitive",
                PrimitiveType = "sphere",
                PrimitiveSize = new[] { 1f, 1f, 1f },
                Resolution    = 8,
                Operation     = "union",
                SavePath      = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Expression mode rejected ─────────────────────────────────────

        [Test]
        public void ExpressionSource_ReturnsNotYetSupportedError()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Source     = "expression",
                Expression = "length(p) - 1.0",
                Resolution = 8,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Mesh source missing MeshPath returns error ───────────────────

        [Test]
        public void MeshSource_MissingMeshPath_ReturnsError()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Source     = "mesh",
                Resolution = 8,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Invalid SavePath rejected ────────────────────────────────────

        [Test]
        public void InvalidSavePath_ReturnsError()
        {
            var result = ProcGenSdfGenerateTool.Execute(new ProcGenSdfGenerateParams
            {
                Source        = "primitive",
                PrimitiveType = "sphere",
                PrimitiveSize = new[] { 1f, 1f, 1f },
                Resolution    = 8,
                SavePath      = "/tmp/not-assets/"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
