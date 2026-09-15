using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Audio;

namespace Mosaic.Bridge.Tests.Unit.Tools.Audio
{
    // O4 §4.4: audio/waveform-image — slides/labs showing compressed vs PCM, loop points.
    [TestFixture]
    [Category("Audio")]
    public class AudioWaveformImageToolTests
    {
        private const string ClipPath = "Assets/MosaicWaveformTestClip.wav";
        private const string ImagePath = "Assets/MosaicWaveformTestImage.png";

        [SetUp]
        public void SetUp()
        {
            var result = AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = ClipPath, Waveform = "tone", DurationSeconds = 0.5f,
                Frequency = 440f, SampleRate = 8000, Channels = 1,
            });
            Assert.IsTrue(result.Success, result.Error);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.AssetPathExists(ClipPath)) AssetDatabase.DeleteAsset(ClipPath);
            if (AssetDatabase.AssetPathExists(ImagePath)) AssetDatabase.DeleteAsset(ImagePath);
        }

        [Test]
        public void GeneratesPngOfRequestedSize()
        {
            var result = AudioWaveformImageTool.Execute(new AudioWaveformImageParams
            {
                AssetPath = ClipPath, OutputPath = ImagePath, Width = 200, Height = 64,
            });

            Assert.IsTrue(result.Success, result.Error);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
            Assert.IsNotNull(texture);
            Assert.AreEqual(200, texture.width);
            Assert.AreEqual(64, texture.height);
        }

        [Test]
        public void CustomColors_AreApplied()
        {
            var result = AudioWaveformImageTool.Execute(new AudioWaveformImageParams
            {
                AssetPath = ClipPath, OutputPath = ImagePath, Width = 64, Height = 32,
                WaveformColor = new[] { 1f, 0f, 0f, 1f },
                BackgroundColor = new[] { 0f, 0f, 1f, 1f },
            });

            Assert.IsTrue(result.Success, result.Error);

            var textureAsset = AssetImporter.GetAtPath(ImagePath) as TextureImporter;
            textureAsset.isReadable = true;
            textureAsset.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);

            bool hasRed = false, hasBlue = false;
            var pixels = texture.GetPixels();
            foreach (var px in pixels)
            {
                if (px.r > 0.9f && px.g < 0.1f && px.b < 0.1f) hasRed = true;
                if (px.b > 0.9f && px.r < 0.1f && px.g < 0.1f) hasBlue = true;
            }
            Assert.IsTrue(hasRed, "expected at least one red (waveform) pixel");
            Assert.IsTrue(hasBlue, "expected at least one blue (background) pixel");
        }

        [Test]
        public void StreamingLoadType_ReturnsNotPermitted()
        {
            AudioSetImportSettingsTool.Execute(new AudioSetImportSettingsParams
            {
                AssetPath = ClipPath, LoadType = "Streaming",
            });

            var result = AudioWaveformImageTool.Execute(new AudioWaveformImageParams
            {
                AssetPath = ClipPath, OutputPath = ImagePath,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_PERMITTED", result.ErrorCode);
        }

        [Test]
        public void NotFound_ReturnsNotFound()
        {
            var result = AudioWaveformImageTool.Execute(new AudioWaveformImageParams
            {
                AssetPath = "Assets/DoesNotExist.wav", OutputPath = ImagePath,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}
