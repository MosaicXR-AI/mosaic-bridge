using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Audio;

namespace Mosaic.Bridge.Tests.Unit.Tools.Audio
{
    // O4 §4.4: audio/synthesize-clip — placeholder tone/noise/sweep/click generation so a course
    // can author "a footstep" or "a chime" on day 1 without waiting on Unity AI generation.
    [TestFixture]
    [Category("Audio")]
    public class AudioSynthesizeClipToolTests
    {
        private const string AssetPath = "Assets/MosaicSynthTestClip.wav";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.AssetPathExists(AssetPath)) AssetDatabase.DeleteAsset(AssetPath);
        }

        [Test]
        public void Tone_GeneratesImportableClip()
        {
            var result = AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = AssetPath, Waveform = "tone", DurationSeconds = 0.5f,
                Frequency = 440f, SampleRate = 8000, Channels = 1,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(4000, result.Data.SampleCount);

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetPath);
            Assert.IsNotNull(clip);
            Assert.AreEqual(1, clip.channels);
            Assert.AreEqual(8000, clip.frequency);
        }

        [Test]
        public void Noise_SameSeed_IsDeterministic()
        {
            AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = AssetPath, Waveform = "noise", DurationSeconds = 0.1f,
                SampleRate = 8000, Seed = 42,
            });
            var clip1 = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetPath);
            var data1 = new float[clip1.samples];
            clip1.GetData(data1, 0);

            AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = AssetPath, Waveform = "noise", DurationSeconds = 0.1f,
                SampleRate = 8000, Seed = 42,
            });
            var clip2 = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetPath);
            var data2 = new float[clip2.samples];
            clip2.GetData(data2, 0);

            Assert.AreEqual(data1, data2);
        }

        [Test]
        public void Sweep_ProducesNonZeroSamples()
        {
            var result = AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = AssetPath, Waveform = "sweep", DurationSeconds = 0.2f,
                Frequency = 200f, EndFrequency = 2000f, SampleRate = 8000,
            });

            Assert.IsTrue(result.Success, result.Error);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetPath);
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            Assert.IsTrue(System.Array.Exists(data, s => Mathf.Abs(s) > 0.01f));
        }

        [Test]
        public void Click_Generates()
        {
            var result = AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = AssetPath, Waveform = "click", DurationSeconds = 0.05f, SampleRate = 8000,
            });

            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void UnknownWaveform_ReturnsInvalidParam()
        {
            var result = AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = AssetPath, Waveform = "orchestral",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void PathOutsideAssets_ReturnsInvalidParam()
        {
            var result = AudioSynthesizeClipTool.Execute(new AudioSynthesizeClipParams
            {
                AssetPath = "Temp/OutsideAssets.wav", Waveform = "tone",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
