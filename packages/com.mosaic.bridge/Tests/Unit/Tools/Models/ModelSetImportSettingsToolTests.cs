using System.IO;
using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.Models;

namespace Mosaic.Bridge.Tests.Unit.Tools.Models
{
    // O4 §4.5 P2: FBX/model import ("character day 1: Mixamo FBX as Humanoid", "why is my model
    // pink / 100x too big" week-1 lab). A minimal OBJ triangle stands in for a real FBX here — OBJ
    // goes through the same ModelImporter, so importer-property round-tripping (scale, compression,
    // readability, material mode, animation type/avatar setup, split clips) is exercised for real
    // without needing a binary FBX fixture with actual rig/animation data checked into the repo.
    [TestFixture]
    [Category("Models")]
    public class ModelSetImportSettingsToolTests
    {
        private const string TestModelPath = "Assets/MosaicBridgeTests_TempModel.obj";

        [SetUp]
        public void SetUp()
        {
            File.WriteAllText(TestModelPath,
                "v 0.0 0.0 0.0\nv 1.0 0.0 0.0\nv 0.0 1.0 0.0\nf 1 2 3\n");
            AssetDatabase.ImportAsset(TestModelPath, ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestModelPath);
        }

        [Test]
        public void SetImportSettings_AppliesScaleAndMeshSettings_AndPersists()
        {
            var result = ModelSetImportSettingsTool.SetImportSettings(new ModelSetImportSettingsParams
            {
                AssetPath = TestModelPath,
                GlobalScale = 0.01f,
                MeshCompression = "High",
                IsReadable = true,
                AddCollider = true,
                GenerateSecondaryUV = true,
                MaterialImportMode = "None",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.01f, result.Data.GlobalScale, 0.0001f);
            Assert.AreEqual("High", result.Data.MeshCompression);
            Assert.IsTrue(result.Data.IsReadable);
            Assert.IsTrue(result.Data.AddCollider);
            Assert.IsTrue(result.Data.GenerateSecondaryUV);
            Assert.AreEqual("None", result.Data.MaterialImportMode);

            var reloaded = (ModelImporter)AssetImporter.GetAtPath(TestModelPath);
            Assert.AreEqual(0.01f, reloaded.globalScale, 0.0001f);
            Assert.AreEqual(ModelImporterMeshCompression.High, reloaded.meshCompression);
            Assert.IsTrue(reloaded.isReadable);
            Assert.IsTrue(reloaded.addCollider);
        }

        [Test]
        public void SetImportSettings_AnimationTypeAndAvatarSetup_RoundTrip()
        {
            // Generic (not Human) — the fixture has no skeleton, and Human + CreateFromThisModel
            // would fail Unity's own rig validation ("Required human bone 'Hips' not found").
            var result = ModelSetImportSettingsTool.SetImportSettings(new ModelSetImportSettingsParams
            {
                AssetPath = TestModelPath,
                AnimationType = "Generic",
                AvatarSetup = "NoAvatar",
                ImportAnimation = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Generic", result.Data.AnimationType);
            Assert.AreEqual("NoAvatar", result.Data.AvatarSetup);
            Assert.IsTrue(result.Data.ImportAnimation);
        }

        [Test]
        public void SetImportSettings_SplitsClips_AndCountRoundTrips()
        {
            var result = ModelSetImportSettingsTool.SetImportSettings(new ModelSetImportSettingsParams
            {
                AssetPath = TestModelPath,
                Clips = new[]
                {
                    new ModelClipInput { Name = "Idle", FirstFrame = 0, LastFrame = 30, LoopTime = true },
                    new ModelClipInput { Name = "Walk", FirstFrame = 31, LastFrame = 60, LoopTime = true },
                },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.Data.ClipCount);

            var reloaded = (ModelImporter)AssetImporter.GetAtPath(TestModelPath);
            Assert.AreEqual(2, reloaded.clipAnimations.Length);
            Assert.AreEqual("Idle", reloaded.clipAnimations[0].name);
            Assert.AreEqual(30f, reloaded.clipAnimations[0].lastFrame);
            Assert.AreEqual("Walk", reloaded.clipAnimations[1].name);
        }

        [Test]
        public void SetImportSettings_UnknownMeshCompression_ReturnsFail()
        {
            var result = ModelSetImportSettingsTool.SetImportSettings(new ModelSetImportSettingsParams
            {
                AssetPath = TestModelPath, MeshCompression = "Ultra",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetImportSettings_MissingAsset_ReturnsNotFound()
        {
            var result = ModelSetImportSettingsTool.SetImportSettings(new ModelSetImportSettingsParams
            {
                AssetPath = "Assets/NoSuchModel.fbx",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Info_ReportsAppliedSettings()
        {
            ModelSetImportSettingsTool.SetImportSettings(new ModelSetImportSettingsParams
            {
                AssetPath = TestModelPath,
                GlobalScale = 2f,
                Clips = new[] { new ModelClipInput { Name = "Run", FirstFrame = 0, LastFrame = 10 } },
            });

            var result = ModelInfoTool.Info(new ModelInfoParams { AssetPath = TestModelPath });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2f, result.Data.GlobalScale, 0.0001f);
            Assert.AreEqual(1, result.Data.Clips.Length);
            Assert.AreEqual("Run", result.Data.Clips[0].Name);
        }

        [Test]
        public void Info_MissingAsset_ReturnsNotFound()
        {
            var result = ModelInfoTool.Info(new ModelInfoParams { AssetPath = "Assets/NoSuchModel.fbx" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}
