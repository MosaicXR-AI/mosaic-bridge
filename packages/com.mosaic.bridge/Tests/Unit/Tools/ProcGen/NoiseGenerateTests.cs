using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.ProcGen;

namespace Mosaic.Bridge.Tests.Unit.Tools.ProcGen
{
    [TestFixture]
    [Category("ProcGen")]
    public class NoiseGenerateTests
    {
        private const string TestOutputDir = "Assets/Generated/Noise/Tests/";
        private string _fullOutputDir;

        [SetUp]
        public void SetUp()
        {
            _fullOutputDir = Path.Combine(Application.dataPath, "..", TestOutputDir);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any generated test files
            if (Directory.Exists(_fullOutputDir))
            {
                Directory.Delete(_fullOutputDir, true);

                string metaFile = _fullOutputDir.TrimEnd('/', '\\') + ".meta";
                if (File.Exists(metaFile))
                    File.Delete(metaFile);

                AssetDatabase.Refresh();
            }
        }

        // ── Resolution ─────────────────────────────────────────────────────

        [Test]
        public void Execute_GeneratesTextureWithCorrectResolution()
        {
            int targetRes = 64;
            var result = ProcGenNoiseGenerateTool.Execute(new ProcGenNoiseGenerateParams
            {
                NoiseType  = "perlin",
                Resolution = targetRes,
                Seed       = 42,
                SavePath   = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(targetRes, result.Data.Resolution);
            Assert.IsNotNull(result.Data.TexturePath);

            // Verify file was actually created
            string fullPath = Path.Combine(Application.dataPath, "..", result.Data.TexturePath);
            Assert.IsTrue(File.Exists(fullPath), $"Expected texture file at {fullPath}");
        }

        // ── Determinism ────────────────────────────────────────────────────

        [Test]
        public void Execute_SameSeed_ProducesDeterministicOutput()
        {
            var paramsA = new ProcGenNoiseGenerateParams
            {
                NoiseType  = "simplex",
                Resolution = 32,
                Seed       = 12345,
                Output     = "float_array",
                SavePath   = TestOutputDir
            };

            var paramsB = new ProcGenNoiseGenerateParams
            {
                NoiseType  = "simplex",
                Resolution = 32,
                Seed       = 12345,
                Output     = "float_array",
                SavePath   = TestOutputDir
            };

            var resultA = ProcGenNoiseGenerateTool.Execute(paramsA);
            var resultB = ProcGenNoiseGenerateTool.Execute(paramsB);

            Assert.IsTrue(resultA.Success, resultA.Error);
            Assert.IsTrue(resultB.Success, resultB.Error);

            // Same seed + same params = same range values (proxy for same data)
            Assert.AreEqual(resultA.Data.Range.Min, resultB.Data.Range.Min, 0.0001f);
            Assert.AreEqual(resultA.Data.Range.Max, resultB.Data.Range.Max, 0.0001f);
        }

        // ── Different noise types produce different patterns ──────────────

        [Test]
        public void Execute_DifferentNoiseTypes_ProduceDifferentPatterns()
        {
            float? perlinMin = null, simplexMin = null, cellularMin = null, valueMin = null;

            foreach (var noiseType in new[] { "perlin", "simplex", "cellular", "value" })
            {
                var result = ProcGenNoiseGenerateTool.Execute(new ProcGenNoiseGenerateParams
                {
                    NoiseType  = noiseType,
                    Resolution = 32,
                    Seed       = 999,
                    Output     = "float_array",
                    SavePath   = TestOutputDir
                });

                Assert.IsTrue(result.Success, $"{noiseType} failed: {result.Error}");
                Assert.AreEqual(noiseType, result.Data.NoiseType);

                switch (noiseType)
                {
                    case "perlin":   perlinMin   = result.Data.Range.Min; break;
                    case "simplex":  simplexMin  = result.Data.Range.Min; break;
                    case "cellular": cellularMin = result.Data.Range.Min; break;
                    case "value":    valueMin    = result.Data.Range.Min; break;
                }
            }

            // At least some noise types should produce different raw min values
            // (extremely unlikely all four produce identical ranges)
            bool allSame = Mathf.Approximately(perlinMin.Value, simplexMin.Value)
                        && Mathf.Approximately(simplexMin.Value, cellularMin.Value)
                        && Mathf.Approximately(cellularMin.Value, valueMin.Value);
            Assert.IsFalse(allSame, "All four noise types produced identical range.Min - patterns should differ");
        }

        // ── ApplyToTerrain with non-existent terrain ──────────────────────

        [Test]
        public void Execute_ApplyToNonExistentTerrain_FailsGracefully()
        {
            var result = ProcGenNoiseGenerateTool.Execute(new ProcGenNoiseGenerateParams
            {
                NoiseType      = "perlin",
                Resolution     = 32,
                Seed           = 1,
                Output         = "texture",
                ApplyToTerrain = "NonExistentTerrain_XYZ_12345",
                SavePath       = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
            Assert.IsTrue(result.Error.Contains("NonExistentTerrain_XYZ_12345"));
        }

        // ── Invalid noise type ────────────────────────────────────────────

        [Test]
        public void Execute_InvalidNoiseType_ReturnsError()
        {
            var result = ProcGenNoiseGenerateTool.Execute(new ProcGenNoiseGenerateParams
            {
                NoiseType  = "invalid_noise",
                Resolution = 32,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            Assert.IsTrue(result.Error.Contains("invalid_noise"));
        }

        // ── Missing noise type ────────────────────────────────────────────

        [Test]
        public void Execute_MissingNoiseType_ReturnsError()
        {
            var result = ProcGenNoiseGenerateTool.Execute(new ProcGenNoiseGenerateParams
            {
                Resolution = 32,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Invalid combine mode ──────────────────────────────────────────

        [Test]
        public void Execute_InvalidCombineMode_ReturnsError()
        {
            var result = ProcGenNoiseGenerateTool.Execute(new ProcGenNoiseGenerateParams
            {
                NoiseType   = "perlin",
                CombineMode = "invalid_mode",
                Resolution  = 32,
                SavePath    = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Resolution out of range ───────────────────────────────────────

        [Test]
        public void Execute_ResolutionTooSmall_ReturnsError()
        {
            var result = ProcGenNoiseGenerateTool.Execute(new ProcGenNoiseGenerateParams
            {
                NoiseType  = "perlin",
                Resolution = 1,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
        }

        // ── CombineMode output ────────────────────────────────────────────

        [Test]
        [TestCase("fbm")]
        [TestCase("ridged")]
        [TestCase("turbulence")]
        [TestCase("billow")]
        public void Execute_AllCombineModes_Succeed(string mode)
        {
            var result = ProcGenNoiseGenerateTool.Execute(new ProcGenNoiseGenerateParams
            {
                NoiseType   = "simplex",
                CombineMode = mode,
                Resolution  = 16,
                Seed        = 7,
                Output      = "float_array",
                SavePath    = TestOutputDir
            });

            Assert.IsTrue(result.Success, $"CombineMode '{mode}' failed: {result.Error}");
            Assert.AreEqual(mode, result.Data.CombineMode);
        }
    }
}
