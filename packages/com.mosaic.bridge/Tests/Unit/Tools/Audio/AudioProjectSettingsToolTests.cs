using NUnit.Framework;
using Mosaic.Bridge.Tools.Audio;

namespace Mosaic.Bridge.Tests.Unit.Tools.Audio
{
    // O4 §4.4: audio/project-settings — SerializedObject(ProjectSettings/AudioManager.asset) had no route.
    [TestFixture]
    [Category("Audio")]
    public class AudioProjectSettingsToolTests
    {
        private AudioProjectSettingsResult _original;

        [SetUp]
        public void SetUp()
        {
            // A query with no fields set changes nothing — snapshot current values to restore after each test.
            var result = AudioProjectSettingsTool.Execute(new AudioProjectSettingsParams());
            Assert.IsTrue(result.Success, result.Error);
            _original = result.Data;
        }

        [TearDown]
        public void TearDown()
        {
            AudioProjectSettingsTool.Execute(new AudioProjectSettingsParams
            {
                Volume = _original.Volume,
                RolloffScale = _original.RolloffScale,
                DopplerFactor = _original.DopplerFactor,
                DefaultSpeakerMode = _original.DefaultSpeakerMode,
                SampleRate = _original.SampleRate,
                RequestedDspBufferSize = _original.RequestedDspBufferSize,
                VirtualVoiceCount = _original.VirtualVoiceCount,
                RealVoiceCount = _original.RealVoiceCount,
                SpatializerPlugin = _original.SpatializerPlugin,
                AmbisonicDecoderPlugin = _original.AmbisonicDecoderPlugin,
                DisableAudio = _original.DisableAudio,
                EnableOutputSuspension = _original.EnableOutputSuspension,
                VirtualizeEffects = _original.VirtualizeEffects,
            });
        }

        [Test]
        public void Query_NoChanges_ReportsCurrentValues()
        {
            var result = AudioProjectSettingsTool.Execute(new AudioProjectSettingsParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("No changes (read-only query)", result.Data.Message);
        }

        [Test]
        public void SetVolumeAndDoppler_AppliesAndPersists()
        {
            var result = AudioProjectSettingsTool.Execute(new AudioProjectSettingsParams
            {
                Volume = 0.6f, DopplerFactor = 2f, RolloffScale = 1.5f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.6f, result.Data.Volume, 0.001f);
            Assert.AreEqual(2f, result.Data.DopplerFactor, 0.001f);
            Assert.AreEqual(1.5f, result.Data.RolloffScale, 0.001f);

            var readBack = AudioProjectSettingsTool.Execute(new AudioProjectSettingsParams());
            Assert.AreEqual(0.6f, readBack.Data.Volume, 0.001f);
        }

        [Test]
        public void SetDefaultSpeakerMode_AppliesEnum()
        {
            var result = AudioProjectSettingsTool.Execute(new AudioProjectSettingsParams
            {
                DefaultSpeakerMode = "Mono",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Mono", result.Data.DefaultSpeakerMode);
        }

        [Test]
        public void SetDefaultSpeakerMode_Unknown_ReturnsInvalidParam()
        {
            var result = AudioProjectSettingsTool.Execute(new AudioProjectSettingsParams
            {
                DefaultSpeakerMode = "Dodecaphonic",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetVoiceCounts_Applies()
        {
            var result = AudioProjectSettingsTool.Execute(new AudioProjectSettingsParams
            {
                VirtualVoiceCount = 256, RealVoiceCount = 16,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(256, result.Data.VirtualVoiceCount);
            Assert.AreEqual(16, result.Data.RealVoiceCount);
        }

        [Test]
        public void SetSpatializerPlugin_Unknown_ReturnsInvalidParam()
        {
            var result = AudioProjectSettingsTool.Execute(new AudioProjectSettingsParams
            {
                SpatializerPlugin = "NotARealPlugin_Mosaic",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
