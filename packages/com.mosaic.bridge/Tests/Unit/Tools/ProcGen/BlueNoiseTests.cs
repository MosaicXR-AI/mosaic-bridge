using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.ProcGen;

namespace Mosaic.Bridge.Tests.Unit.Tools.ProcGen
{
    [TestFixture]
    [Category("ProcGen")]
    public class BlueNoiseTests
    {
        private const string TestOutputDir = "Assets/Generated/BlueNoise/Tests/";
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

        // ── Texture output creates file at correct resolution ────────────

        [Test]
        public void TextureOutput_CreatesFileAtCorrectResolution()
        {
            int targetRes = 16;
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Resolution = targetRes,
                Channels   = 1,
                Seed       = 42,
                SavePath   = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(targetRes, result.Data.Resolution);
            Assert.IsNotNull(result.Data.TexturePath);

            string fullPath = Path.Combine(Application.dataPath, "..", result.Data.TexturePath);
            Assert.IsTrue(File.Exists(fullPath), $"Expected texture file at {fullPath}");
        }

        // ── Points output returns correct count ──────────────────────────

        [Test]
        public void PointsOutput_ReturnsCorrectCount()
        {
            int count = 50;
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Output     = "points",
                PointCount = count,
                Seed       = 7
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(count, result.Data.PointCount);
            Assert.IsNotNull(result.Data.Points);
            Assert.AreEqual(count, result.Data.Points.Length);

            // Each point should be [x, y]
            foreach (var pt in result.Data.Points)
                Assert.AreEqual(2, pt.Length, "Each point should have 2 components");
        }

        // ── Deterministic with seed ──────────────────────────────────────

        [Test]
        public void SameSeed_ProducesDeterministicTexture()
        {
            // Use VoidAndCluster directly so we don't create file IO twice
            var rngA = new System.Random(123);
            var rngB = new System.Random(123);

            float[] dataA = ProcGenBlueNoiseTool.VoidAndCluster(8, true, rngA);
            float[] dataB = ProcGenBlueNoiseTool.VoidAndCluster(8, true, rngB);

            Assert.AreEqual(dataA.Length, dataB.Length);
            for (int i = 0; i < dataA.Length; i++)
                Assert.AreEqual(dataA[i], dataB[i], 0.0001f, $"Pixel {i} differs");
        }

        [Test]
        public void SameSeed_ProducesDeterministicPoints()
        {
            var p = new ProcGenBlueNoiseParams
            {
                Output     = "points",
                PointCount = 20,
                Seed       = 999
            };

            var resultA = ProcGenBlueNoiseTool.Execute(p);
            var resultB = ProcGenBlueNoiseTool.Execute(p);

            Assert.IsTrue(resultA.Success, resultA.Error);
            Assert.IsTrue(resultB.Success, resultB.Error);
            Assert.AreEqual(resultA.Data.PointCount, resultB.Data.PointCount);

            for (int i = 0; i < resultA.Data.PointCount; i++)
            {
                Assert.AreEqual(resultA.Data.Points[i][0], resultB.Data.Points[i][0], 0.0001f);
                Assert.AreEqual(resultA.Data.Points[i][1], resultB.Data.Points[i][1], 0.0001f);
            }
        }

        // ── Channels 1-4 all work ────────────────────────────────────────

        [Test]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void TextureOutput_AllChannelCounts_Succeed(int channels)
        {
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Resolution = 8,
                Channels   = channels,
                Seed       = 42,
                SavePath   = TestOutputDir
            });

            Assert.IsTrue(result.Success, $"Channels={channels} failed: {result.Error}");
            Assert.AreEqual(channels, result.Data.Channels);
            Assert.IsNotNull(result.Data.TexturePath);
        }

        // ── Invalid resolution returns error ─────────────────────────────

        [Test]
        public void TextureOutput_ResolutionTooSmall_ReturnsError()
        {
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Resolution = 1,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
        }

        [Test]
        public void TextureOutput_ResolutionTooLarge_ReturnsError()
        {
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Resolution = 8192,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
        }

        // ── Invalid channels returns error ───────────────────────────────

        [Test]
        public void TextureOutput_ChannelsTooHigh_ReturnsError()
        {
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Resolution = 8,
                Channels   = 5,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
        }

        [Test]
        public void TextureOutput_ChannelsTooLow_ReturnsError()
        {
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Resolution = 8,
                Channels   = 0,
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
        }

        // ── Invalid output mode ──────────────────────────────────────────

        [Test]
        public void InvalidOutput_ReturnsError()
        {
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Output = "invalid_mode"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Points output without PointCount ─────────────────────────────

        [Test]
        public void PointsOutput_MissingPointCount_ReturnsError()
        {
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Output = "points"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Points within bounds ─────────────────────────────────────────

        [Test]
        public void PointsOutput_AllPointsWithinBounds()
        {
            float[] bMin = { 10f, 20f };
            float[] bMax = { 30f, 40f };

            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Output     = "points",
                PointCount = 100,
                BoundsMin  = bMin,
                BoundsMax  = bMax,
                Seed       = 42
            });

            Assert.IsTrue(result.Success, result.Error);

            foreach (var pt in result.Data.Points)
            {
                Assert.GreaterOrEqual(pt[0], bMin[0], "Point X below BoundsMin");
                Assert.LessOrEqual(pt[0], bMax[0], "Point X above BoundsMax");
                Assert.GreaterOrEqual(pt[1], bMin[1], "Point Y below BoundsMin");
                Assert.LessOrEqual(pt[1], bMax[1], "Point Y above BoundsMax");
            }
        }

        // ── Invalid bounds for points ────────────────────────────────────

        [Test]
        public void PointsOutput_InvalidBounds_ReturnsError()
        {
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Output     = "points",
                PointCount = 10,
                BoundsMin  = new[] { 10f, 10f },
                BoundsMax  = new[] { 5f, 5f }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Tiling produces seamless edges ───────────────────────────────
        //    Edge pixels should have similar value distribution to center pixels.
        //    A non-tiling texture would have suppressed values at edges.

        [Test]
        public void TilingTexture_EdgeDistributionSimilarToCenter()
        {
            int res = 32;
            var rng = new System.Random(77);
            float[] data = ProcGenBlueNoiseTool.VoidAndCluster(res, true, rng);

            // Compute average value along edges vs center strip
            float edgeSum = 0f;
            int edgeCount = 0;
            float centerSum = 0f;
            int centerCount = 0;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float v = data[y * res + x];
                    bool isEdge = (x == 0 || x == res - 1 || y == 0 || y == res - 1);
                    bool isCenter = (x >= res / 4 && x < 3 * res / 4 &&
                                     y >= res / 4 && y < 3 * res / 4);

                    if (isEdge) { edgeSum += v; edgeCount++; }
                    if (isCenter) { centerSum += v; centerCount++; }
                }
            }

            float edgeAvg = edgeSum / edgeCount;
            float centerAvg = centerSum / centerCount;

            // For a well-distributed blue noise with tiling, edge and center
            // averages should both be close to 0.5. Allow generous tolerance.
            Assert.AreEqual(0.5f, edgeAvg, 0.15f,
                $"Edge average {edgeAvg:F3} deviates too much from 0.5");
            Assert.AreEqual(0.5f, centerAvg, 0.15f,
                $"Center average {centerAvg:F3} deviates too much from 0.5");

            // Edge and center should not differ dramatically
            float diff = Mathf.Abs(edgeAvg - centerAvg);
            Assert.Less(diff, 0.2f,
                $"Edge avg ({edgeAvg:F3}) and center avg ({centerAvg:F3}) differ by {diff:F3}");
        }

        // ── Texture values are normalized [0, 1] ─────────────────────────

        [Test]
        public void VoidAndCluster_OutputIsNormalized()
        {
            int res = 16;
            var rng = new System.Random(55);
            float[] data = ProcGenBlueNoiseTool.VoidAndCluster(res, true, rng);

            Assert.AreEqual(res * res, data.Length);

            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (float v in data)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }

            Assert.GreaterOrEqual(min, 0f, "Minimum value should be >= 0");
            Assert.LessOrEqual(max, 1f, "Maximum value should be <= 1");
            // Should use full range
            Assert.Less(min, 0.01f, "Minimum should be near 0");
            Assert.Greater(max, 0.99f, "Maximum should be near 1");
        }

        // ── Invalid SavePath ─────────────────────────────────────────────

        [Test]
        public void TextureOutput_InvalidSavePath_ReturnsError()
        {
            var result = ProcGenBlueNoiseTool.Execute(new ProcGenBlueNoiseParams
            {
                Resolution = 8,
                SavePath   = "/tmp/not-assets/"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
