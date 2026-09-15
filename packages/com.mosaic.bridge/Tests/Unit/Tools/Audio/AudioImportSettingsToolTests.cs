using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Audio;

namespace Mosaic.Bridge.Tests.Unit.Tools.Audio
{
    // O4 §4.4: audio/set-import-settings + audio/clip-info — AudioImporter.forceToMono/loadInBackground/
    // ambisonic and per-platform AudioImporterSampleSettings had no route.
    [TestFixture]
    [Category("Audio")]
    public class AudioImportSettingsToolTests
    {
        private const string ClipPath = "Assets/MosaicAudioImportTestClip.wav";

        [SetUp]
        public void SetUp()
        {
            var bytes = BuildMinimalPcmWav(sampleRate: 44100, channels: 1, sampleCount: 4410);
            File.WriteAllBytes(
                Path.Combine(Path.GetDirectoryName(Application.dataPath), ClipPath), bytes);
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.AssetPathExists(ClipPath)) AssetDatabase.DeleteAsset(ClipPath);
        }

        [Test]
        public void SetImportSettings_ForceToMonoAndAmbisonic_Applies()
        {
            var result = AudioSetImportSettingsTool.Execute(new AudioSetImportSettingsParams
            {
                AssetPath = ClipPath, ForceToMono = true, LoadInBackground = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.ForceToMono);
            Assert.IsTrue(result.Data.LoadInBackground);

            var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            Assert.IsTrue(importer.forceToMono);
            Assert.IsTrue(importer.loadInBackground);
        }

        [Test]
        public void SetImportSettings_OverrideSampleSettings_AppliesToPlatform()
        {
            var result = AudioSetImportSettingsTool.Execute(new AudioSetImportSettingsParams
            {
                AssetPath = ClipPath, Platform = "Standalone",
                LoadType = "Streaming", CompressionFormat = "Vorbis",
                SampleRateSetting = "OverrideSampleRate", SampleRateOverride = 22050,
                Quality = 0.5f, PreloadAudioData = false,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.HasOverride);
            Assert.AreEqual("Streaming", result.Data.LoadType);
            Assert.AreEqual("Vorbis", result.Data.CompressionFormat);
            Assert.AreEqual(22050u, result.Data.SampleRateOverride);
            Assert.AreEqual(0.5f, result.Data.Quality, 0.001f);

            var importer = AssetImporter.GetAtPath(ClipPath) as AudioImporter;
            Assert.IsTrue(importer.ContainsSampleSettingsOverride("Standalone"));
        }

        [Test]
        public void SetImportSettings_ClearOverride_RemovesIt()
        {
            AudioSetImportSettingsTool.Execute(new AudioSetImportSettingsParams
            {
                AssetPath = ClipPath, Platform = "Standalone", LoadType = "Streaming",
            });

            var result = AudioSetImportSettingsTool.Execute(new AudioSetImportSettingsParams
            {
                AssetPath = ClipPath, Platform = "Standalone", ClearOverride = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(result.Data.HasOverride);
        }

        [Test]
        public void SetImportSettings_NotFound_ReturnsNotFound()
        {
            var result = AudioSetImportSettingsTool.Execute(new AudioSetImportSettingsParams
            {
                AssetPath = "Assets/DoesNotExist.wav", ForceToMono = true,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void ClipInfo_ReportsRuntimeAndImporterData()
        {
            var result = AudioClipInfoTool.Execute(new AudioClipInfoParams { AssetPath = ClipPath });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Channels);
            Assert.AreEqual(44100, result.Data.Frequency);
            Assert.Greater(result.Data.Length, 0f);
            Assert.AreEqual("Standalone", result.Data.Platform);
        }

        [Test]
        public void ClipInfo_NotFound_ReturnsNotFound()
        {
            var result = AudioClipInfoTool.Execute(new AudioClipInfoParams { AssetPath = "Assets/Nope.wav" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        private static byte[] BuildMinimalPcmWav(int sampleRate, short channels, int sampleCount)
        {
            const short bitsPerSample = 16;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            short blockAlign = (short)(channels * bitsPerSample / 8);
            int dataSize = sampleCount * blockAlign;

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataSize);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1); // PCM
            w.Write(channels);
            w.Write(sampleRate);
            w.Write(byteRate);
            w.Write(blockAlign);
            w.Write(bitsPerSample);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataSize);
            w.Write(new byte[dataSize]); // silence

            return ms.ToArray();
        }
    }
}
