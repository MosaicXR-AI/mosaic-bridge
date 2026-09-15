using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.Audio;

namespace Mosaic.Bridge.Tests.Unit.Tools.Audio
{
    // O4 §4.4: audio/preview — verify a generated clip is audible/correct length before wiring it
    // up. UnityEditor.AudioUtil is internal but has been a stable "extern public static" binding
    // for years, the doc's lowest-risk internal-API item.
    [TestFixture]
    [Category("Audio")]
    public class AudioPreviewToolTests
    {
        private const string ClipPath = "Assets/MosaicPreviewTestClip.wav";

        [SetUp]
        public void SetUp()
        {
            AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = ClipPath, Waveform = "tone", DurationSeconds = 0.2f, SampleRate = 8000,
            });
        }

        [TearDown]
        public void TearDown()
        {
            AudioPreviewTool.Execute(new AudioPreviewParams { Action = "stop" });
            if (AssetDatabase.AssetPathExists(ClipPath)) AssetDatabase.DeleteAsset(ClipPath);
        }

        [Test]
        public void Play_ValidClip_Succeeds()
        {
            var result = AudioPreviewTool.Execute(new AudioPreviewParams { Action = "play", ClipPath = ClipPath });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.IsPlaying);
        }

        [Test]
        public void Play_MissingClipPath_ReturnsInvalidParam()
        {
            var result = AudioPreviewTool.Execute(new AudioPreviewParams { Action = "play" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Play_NotFound_ReturnsNotFound()
        {
            var result = AudioPreviewTool.Execute(new AudioPreviewParams
            {
                Action = "play", ClipPath = "Assets/DoesNotExist.wav",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Stop_Succeeds()
        {
            AudioPreviewTool.Execute(new AudioPreviewParams { Action = "play", ClipPath = ClipPath });

            var result = AudioPreviewTool.Execute(new AudioPreviewParams { Action = "stop" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(result.Data.IsPlaying);
        }

        [Test]
        public void Status_AfterStop_ReportsNotPlaying()
        {
            AudioPreviewTool.Execute(new AudioPreviewParams { Action = "stop" });

            var result = AudioPreviewTool.Execute(new AudioPreviewParams { Action = "status" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(result.Data.IsPlaying);
        }

        [Test]
        public void UnknownAction_ReturnsInvalidParam()
        {
            var result = AudioPreviewTool.Execute(new AudioPreviewParams { Action = "rewind" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
