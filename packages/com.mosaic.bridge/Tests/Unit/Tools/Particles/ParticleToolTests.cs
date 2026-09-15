using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Particles;
using Mosaic.Bridge.Contracts.Compat;

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

            const string dir = "Assets/Generated/ParticleMaterials";
            if (UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.DeleteAsset(dir);
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

        // ── particle/set-renderer ────────────────────────────────────────────

        // L12: `new Material(shader)` alone is a pure in-memory object never written to disk —
        // it reads back as pink/missing the moment the project reloads. It must be a real asset.
        [Test]
        public void SetRenderer_UseUrpParticlesMaterial_SavesARealAsset()
        {
            var createResult = ParticleCreateTool.Execute(new ParticleCreateParams { Name = "UrpMatPS" });
            Assert.IsTrue(createResult.Success, createResult.Error);
            _created = FindByInstanceId(createResult.Data.InstanceId);

            var result = ParticleSetRendererTool.Execute(new ParticleSetRendererParams
            {
                Name = "UrpMatPS", UseUrpParticlesMaterial = true
            });

            Assert.IsTrue(result.Success, result.Error);
            var renderer = _created.GetComponent<ParticleSystemRenderer>();
            Assert.IsNotNull(renderer.sharedMaterial);
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(renderer.sharedMaterial);
            Assert.IsFalse(string.IsNullOrEmpty(assetPath),
                "the material must be a real project asset, not an unsaved in-memory Material");
            Assert.IsTrue(assetPath.EndsWith(".mat"));
        }

        [Test]
        public void SetRenderer_UseUrpParticlesMaterial_CalledTwice_ReusesTheSameAsset()
        {
            var createResult = ParticleCreateTool.Execute(new ParticleCreateParams { Name = "UrpMatPS2" });
            Assert.IsTrue(createResult.Success, createResult.Error);
            _created = FindByInstanceId(createResult.Data.InstanceId);

            ParticleSetRendererTool.Execute(new ParticleSetRendererParams
            {
                Name = "UrpMatPS2", UseUrpParticlesMaterial = true
            });
            var firstMat = _created.GetComponent<ParticleSystemRenderer>().sharedMaterial;

            ParticleSetRendererTool.Execute(new ParticleSetRendererParams
            {
                Name = "UrpMatPS2", UseUrpParticlesMaterial = true
            });
            var secondMat = _created.GetComponent<ParticleSystemRenderer>().sharedMaterial;

            Assert.AreSame(firstMat, secondMat, "repeated calls must not create duplicate assets");
        }

        // O4 §4.8: renderer gaps — MeshPath, TrailMaterialPath, SortingLayer, SortingOrder.
        [Test]
        public void SetRenderer_SortingLayerAndOrder_Apply()
        {
            var createResult = ParticleCreateTool.Execute(new ParticleCreateParams { Name = "SortingPS" });
            Assert.IsTrue(createResult.Success, createResult.Error);
            _created = FindByInstanceId(createResult.Data.InstanceId);

            var result = ParticleSetRendererTool.Execute(new ParticleSetRendererParams
            {
                Name = "SortingPS", SortingLayer = "Default", SortingOrder = 5,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(5, result.Data.SortingOrder);
            var renderer = _created.GetComponent<ParticleSystemRenderer>();
            Assert.AreEqual(5, renderer.sortingOrder);
            Assert.AreEqual("Default", renderer.sortingLayerName);
        }

        [Test]
        public void SetRenderer_MeshPath_Applies()
        {
            var createResult = ParticleCreateTool.Execute(new ParticleCreateParams { Name = "MeshPS" });
            Assert.IsTrue(createResult.Success, createResult.Error);
            _created = FindByInstanceId(createResult.Data.InstanceId);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = cube.GetComponent<MeshFilter>().sharedMesh;
            const string meshPath = "Assets/MosaicParticleTestMesh.asset";
            UnityEditor.AssetDatabase.CreateAsset(Object.Instantiate(mesh), meshPath);
            Object.DestroyImmediate(cube);

            try
            {
                var result = ParticleSetRendererTool.Execute(new ParticleSetRendererParams
                {
                    Name = "MeshPS", RenderMode = "Mesh", MeshPath = meshPath,
                });

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual(meshPath, result.Data.MeshPath);
                Assert.IsNotNull(_created.GetComponent<ParticleSystemRenderer>().mesh);
            }
            finally
            {
                if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(meshPath) != null)
                    UnityEditor.AssetDatabase.DeleteAsset(meshPath);
            }
        }

        [Test]
        public void SetRenderer_MeshPathNotFound_ReturnsNotFound()
        {
            var createResult = ParticleCreateTool.Execute(new ParticleCreateParams { Name = "MeshPS2" });
            Assert.IsTrue(createResult.Success, createResult.Error);
            _created = FindByInstanceId(createResult.Data.InstanceId);

            var result = ParticleSetRendererTool.Execute(new ParticleSetRendererParams
            {
                Name = "MeshPS2", MeshPath = "Assets/DoesNotExist.asset",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static GameObject FindByInstanceId(int instanceId)
        {
            return UnityIds.Resolve(instanceId) as GameObject;
        }
    }
}
