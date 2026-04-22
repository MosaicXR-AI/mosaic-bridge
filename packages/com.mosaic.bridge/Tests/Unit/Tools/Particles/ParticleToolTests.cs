using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Particles;

namespace Mosaic.Bridge.Tests.Unit.Tools.Particles
{
    [TestFixture]
    [Category("Unit")]
    [Category("Particle")]
    public class ParticleToolTests
    {
        private GameObject _created;

        [TearDown]
        public void TearDown()
        {
            if (_created != null)
                Object.DestroyImmediate(_created);
            _created = null;
        }

        // ── particle/create ─────────────────────────────────────────────────

        [Test]
        public void Create_Default_ReturnsParticleSystem()
        {
            var result = ParticleCreateTool.Execute(new ParticleCreateParams());
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("Particle System", result.Data.Name);

            _created = FindByInstanceId(result.Data.InstanceId);
            Assert.IsNotNull(_created);
            Assert.IsNotNull(_created.GetComponent<ParticleSystem>());
        }

        [Test]
        public void Create_WithName_UsesCustomName()
        {
            var result = ParticleCreateTool.Execute(new ParticleCreateParams
            {
                Name = "MyFX"
            });
            Assert.IsTrue(result.Success);
            Assert.AreEqual("MyFX", result.Data.Name);

            _created = FindByInstanceId(result.Data.InstanceId);
        }

        [Test]
        public void Create_FirePreset_HasUpwardVelocityAndGravity()
        {
            var result = ParticleCreateTool.Execute(new ParticleCreateParams
            {
                Preset = "fire"
            });
            Assert.IsTrue(result.Success);
            Assert.AreEqual("fire", result.Data.Preset);

            _created = FindByInstanceId(result.Data.InstanceId);
            var ps = _created.GetComponent<ParticleSystem>();
            var main = ps.main;
            // Fire preset has negative gravity (upward drift)
            Assert.Less(main.gravityModifier.constant, 0f,
                "Fire preset should have negative gravity modifier for upward drift");
        }

        // ── particle/set-main ───────────────────────────────────────────────

        [Test]
        public void SetMain_Duration_UpdatesValue()
        {
            // Create a particle system first
            var createResult = ParticleCreateTool.Execute(new ParticleCreateParams { Name = "TestPS" });
            Assert.IsTrue(createResult.Success);
            _created = FindByInstanceId(createResult.Data.InstanceId);

            var result = ParticleSetMainTool.Execute(new ParticleSetMainParams
            {
                Name = "TestPS",
                Duration = 10f
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(10f, result.Data.Duration, 0.01f);

            var ps = _created.GetComponent<ParticleSystem>();
            Assert.AreEqual(10f, ps.main.duration, 0.01f);
        }

        [Test]
        public void SetMain_MissingTarget_ReturnsFail()
        {
            var result = ParticleSetMainTool.Execute(new ParticleSetMainParams());
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetMain_NotFound_ReturnsFail()
        {
            var result = ParticleSetMainTool.Execute(new ParticleSetMainParams
            {
                Name = "NonExistentParticleSystem_12345"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // ── particle/info ───────────────────────────────────────────────────

        [Test]
        public void Info_SpecificSystem_ReturnsProperties()
        {
            var createResult = ParticleCreateTool.Execute(new ParticleCreateParams
            {
                Name = "InfoTestPS"
            });
            Assert.IsTrue(createResult.Success);
            _created = FindByInstanceId(createResult.Data.InstanceId);

            var result = ParticleInfoTool.Execute(new ParticleInfoParams
            {
                Name = "InfoTestPS"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.TotalCount);
            Assert.AreEqual("InfoTestPS", result.Data.ParticleSystems[0].Name);
            Assert.IsNotNull(result.Data.ParticleSystems[0].Shape);
        }

        // ── particle/playback ───────────────────────────────────────────────

        [Test]
        public void Playback_Stop_SetsNotPlaying()
        {
            var createResult = ParticleCreateTool.Execute(new ParticleCreateParams
            {
                Name = "PlaybackTestPS"
            });
            Assert.IsTrue(createResult.Success);
            _created = FindByInstanceId(createResult.Data.InstanceId);

            var result = ParticlePlaybackTool.Execute(new ParticlePlaybackParams
            {
                Name = "PlaybackTestPS",
                Action = "stop"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("stop", result.Data.Action);
            Assert.IsFalse(result.Data.IsPlaying);
        }

        [Test]
        public void Playback_InvalidAction_ReturnsFail()
        {
            var createResult = ParticleCreateTool.Execute(new ParticleCreateParams
            {
                Name = "PlaybackFailPS"
            });
            Assert.IsTrue(createResult.Success);
            _created = FindByInstanceId(createResult.Data.InstanceId);

            var result = ParticlePlaybackTool.Execute(new ParticlePlaybackParams
            {
                Name = "PlaybackFailPS",
                Action = "invalid_action"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static GameObject FindByInstanceId(int instanceId)
        {
            return UnityEngine.Resources.EntityIdToObject(instanceId) as GameObject;
        }
    }
}
