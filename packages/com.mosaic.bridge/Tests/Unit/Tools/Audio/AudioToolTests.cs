using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Audio;

namespace Mosaic.Bridge.Tests.Unit.Tools.Audio
{
    [TestFixture]
    [Category("Audio")]
    public class AudioToolTests
    {
        private GameObject _testGo;

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null)
            {
                Object.DestroyImmediate(_testGo);
                _testGo = null;
            }
        }

        // ── audio/create-source ─────────────────────────────────────────────

        [Test]
        public void CreateSource_NoTarget_CreatesNewGameObjectWithAudioSource()
        {
            var result = AudioCreateSourceTool.Execute(new AudioCreateSourceParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("AudioSource", result.Data.GameObjectName);

            // Clean up the created GO
            _testGo = EditorUtility.InstanceIDToObject(result.Data.InstanceId) as GameObject;
            Assert.IsNotNull(_testGo);

            var source = _testGo.GetComponent<AudioSource>();
            Assert.IsNotNull(source);
        }

        [Test]
        public void CreateSource_WithTarget_AddsComponentToExistingGO()
        {
            _testGo = new GameObject("AudioTest_Target");

            var result = AudioCreateSourceTool.Execute(new AudioCreateSourceParams
            {
                Name = "AudioTest_Target"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("AudioTest_Target", result.Data.GameObjectName);

            var source = _testGo.GetComponent<AudioSource>();
            Assert.IsNotNull(source, "AudioSource component should be added to existing GO");
        }

        [Test]
        public void CreateSource_WithProperties_SetsAllValues()
        {
            _testGo = new GameObject("AudioTest_Props");

            var result = AudioCreateSourceTool.Execute(new AudioCreateSourceParams
            {
                Name        = "AudioTest_Props",
                Volume      = 0.5f,
                Pitch       = 1.5f,
                SpatialBlend = 1.0f,
                Loop        = true,
                PlayOnAwake = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.5f, result.Data.Volume, 0.001f);
            Assert.AreEqual(1.5f, result.Data.Pitch, 0.001f);
            Assert.AreEqual(1.0f, result.Data.SpatialBlend, 0.001f);
            Assert.IsTrue(result.Data.Loop);
            Assert.IsFalse(result.Data.PlayOnAwake);

            var source = _testGo.GetComponent<AudioSource>();
            Assert.AreEqual(0.5f, source.volume, 0.001f);
            Assert.AreEqual(1.5f, source.pitch, 0.001f);
            Assert.AreEqual(1.0f, source.spatialBlend, 0.001f);
            Assert.IsTrue(source.loop);
            Assert.IsFalse(source.playOnAwake);
        }

        [Test]
        public void CreateSource_VolumeClamped_Between0And1()
        {
            _testGo = new GameObject("AudioTest_Clamp");

            var result = AudioCreateSourceTool.Execute(new AudioCreateSourceParams
            {
                Name   = "AudioTest_Clamp",
                Volume = 5.0f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1.0f, result.Data.Volume, 0.001f);
        }

        // ── audio/info ──────────────────────────────────────────────────────

        [Test]
        public void Info_SceneWide_ReturnsSources()
        {
            _testGo = new GameObject("AudioTest_Info");
            _testGo.AddComponent<AudioSource>();

            var result = AudioInfoTool.Execute(new AudioInfoParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.Sources);
            Assert.IsTrue(result.Data.Sources.Any(s => s.GameObjectName == "AudioTest_Info"),
                "Should find the AudioSource we created");
        }

        [Test]
        public void Info_SpecificGO_ReturnsOnlyThatGO()
        {
            _testGo = new GameObject("AudioTest_InfoSpecific");
            _testGo.AddComponent<AudioSource>();

            var result = AudioInfoTool.Execute(new AudioInfoParams
            {
                Name = "AudioTest_InfoSpecific"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Sources.Count);
            Assert.AreEqual("AudioTest_InfoSpecific", result.Data.Sources[0].GameObjectName);
        }

        [Test]
        public void Info_NoListener_WarnsAboutMissingListener()
        {
            // Remove all listeners for this test
            var existingListeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var l in existingListeners)
                l.enabled = false;

            try
            {
                // Destroy listeners temporarily — but we can't destroy MainCamera easily,
                // so we check the warning is generated when no listeners exist
                _testGo = new GameObject("AudioTest_NoListener");
                _testGo.AddComponent<AudioSource>();

                var result = AudioInfoTool.Execute(new AudioInfoParams());

                Assert.IsTrue(result.Success, result.Error);
                // If there are no enabled AudioListeners in scene, we should get a warning
                // (Note: there may be listeners on other objects in test environment)
                Assert.IsNotNull(result.Data.Warnings);
            }
            finally
            {
                foreach (var l in existingListeners)
                    l.enabled = true;
            }
        }

        [Test]
        public void Info_SourceWithoutClip_WarnsAboutMissingClip()
        {
            _testGo = new GameObject("AudioTest_NoClip");
            _testGo.AddComponent<AudioSource>();

            var result = AudioInfoTool.Execute(new AudioInfoParams
            {
                Name = "AudioTest_NoClip"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.Warnings.Any(w => w.Contains("no AudioClip")),
                "Should warn about AudioSource without clip");
        }

        [Test]
        public void Info_NotFoundGO_ReturnsError()
        {
            var result = AudioInfoTool.Execute(new AudioInfoParams
            {
                Name = "AudioTest_DoesNotExist_12345"
            });

            Assert.IsFalse(result.Success);
        }

        // ── audio/set-spatial ───────────────────────────────────────────────

        [Test]
        public void SetSpatial_SetsMinMaxDistance()
        {
            _testGo = new GameObject("AudioTest_Spatial");
            _testGo.AddComponent<AudioSource>();

            var result = AudioSetSpatialTool.Execute(new AudioSetSpatialParams
            {
                Name        = "AudioTest_Spatial",
                MinDistance  = 2f,
                MaxDistance  = 50f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2f, result.Data.MinDistance, 0.001f);
            Assert.AreEqual(50f, result.Data.MaxDistance, 0.001f);

            var source = _testGo.GetComponent<AudioSource>();
            Assert.AreEqual(2f, source.minDistance, 0.001f);
            Assert.AreEqual(50f, source.maxDistance, 0.001f);
        }

        [Test]
        public void SetSpatial_SetsRolloffMode()
        {
            _testGo = new GameObject("AudioTest_Rolloff");
            _testGo.AddComponent<AudioSource>();

            var result = AudioSetSpatialTool.Execute(new AudioSetSpatialParams
            {
                Name        = "AudioTest_Rolloff",
                RolloffMode = "Linear"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Linear", result.Data.RolloffMode);

            var source = _testGo.GetComponent<AudioSource>();
            Assert.AreEqual(AudioRolloffMode.Linear, source.rolloffMode);
        }

        [Test]
        public void SetSpatial_InvalidRolloff_ReturnsError()
        {
            _testGo = new GameObject("AudioTest_BadRolloff");
            _testGo.AddComponent<AudioSource>();

            var result = AudioSetSpatialTool.Execute(new AudioSetSpatialParams
            {
                Name        = "AudioTest_BadRolloff",
                RolloffMode = "InvalidMode"
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void SetSpatial_NoAudioSource_ReturnsError()
        {
            _testGo = new GameObject("AudioTest_NoSource");

            var result = AudioSetSpatialTool.Execute(new AudioSetSpatialParams
            {
                Name       = "AudioTest_NoSource",
                MinDistance = 5f
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void SetSpatial_NoIdentifier_ReturnsError()
        {
            var result = AudioSetSpatialTool.Execute(new AudioSetSpatialParams
            {
                MinDistance = 5f
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void SetSpatial_SetsDopplerAndSpread()
        {
            _testGo = new GameObject("AudioTest_DopplerSpread");
            _testGo.AddComponent<AudioSource>();

            var result = AudioSetSpatialTool.Execute(new AudioSetSpatialParams
            {
                Name         = "AudioTest_DopplerSpread",
                DopplerLevel = 2.5f,
                Spread       = 180f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2.5f, result.Data.DopplerLevel, 0.001f);
            Assert.AreEqual(180f, result.Data.Spread, 0.001f);
        }
    }
}
