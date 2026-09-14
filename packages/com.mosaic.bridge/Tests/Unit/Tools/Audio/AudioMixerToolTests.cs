using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEditor;
using Mosaic.Bridge.Tools.Audio;

namespace Mosaic.Bridge.Tests.Unit.Tools.Audio
{
    // O4 §4.4 (G4): AudioMixerController/AudioMixerGroupController are internal — everything here
    // beyond rename (plain Object.name) goes through reflection (AudioMixerReflection). A
    // reflection call can compile cleanly and still be wrong at runtime (wrong overload, subtly
    // different behavior than assumed), which no compile check catches — these tests create a
    // real mixer asset and exercise the real internal API, the only way to actually verify it.
    [TestFixture]
    [Category("Audio")]
    public class AudioMixerToolTests
    {
        private const string TestMixerPath = "Assets/MosaicBridgeTests_TempMixer.mixer";
        private GameObject _testGo;

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null)
            {
                Object.DestroyImmediate(_testGo);
                _testGo = null;
            }
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(TestMixerPath) != null)
                AssetDatabase.DeleteAsset(TestMixerPath);
        }

        [Test]
        public void Reflection_Probe_ReportsReachable()
        {
            var probe = AudioMixerReflection.Probe();

            Assert.IsTrue(probe.Ok, probe.Missing);
        }

        // ── audio/create-mixer ───────────────────────────────────────────────

        [Test]
        public void CreateMixer_CreatesAssetWithMasterGroup()
        {
            var result = AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.Created);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<AudioMixer>(TestMixerPath));
            Assert.AreEqual("Master", result.Data.MasterGroupName);
        }

        [Test]
        public void CreateMixer_Idempotent_SecondCallDoesNotReportCreated()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });

            var result = AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(result.Data.Created);
        }

        [Test]
        public void CreateMixer_RejectsNonMixerExtension()
        {
            var result = AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = "Assets/MosaicBridgeTests_NotAMixer.asset" });

            Assert.IsFalse(result.Success);
        }

        // ── audio/mixer-group ────────────────────────────────────────────────

        [Test]
        public void MixerGroup_Add_CreatesGroupUnderMaster()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });

            var result = AudioMixerGroupTool.Execute(new AudioMixerGroupParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("SFX", result.Data.GroupName);
            Assert.AreEqual("Master", result.Data.ParentName);
        }

        [Test]
        public void MixerGroup_Add_AppearsInMixerInfoHierarchyAndFlatList()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });

            var info = AudioMixerInfoTool.Execute(new AudioMixerInfoParams { MixerAssetPath = TestMixerPath });

            Assert.IsTrue(info.Success, info.Error);
            CollectionAssert.Contains(info.Data.GroupNames, "SFX");
            Assert.IsTrue(info.Data.HierarchyAvailable, info.Data.Note);
            Assert.IsTrue(info.Data.Hierarchy.Children != null && info.Data.Hierarchy.Children.Any(c => c.Name == "SFX"),
                "the new group must appear as a child of the master node in the returned tree");
        }

        [Test]
        public void MixerGroup_Rename_ChangesName()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });

            var result = AudioMixerGroupTool.Execute(new AudioMixerGroupParams
            {
                MixerAssetPath = TestMixerPath, Operation = "rename", GroupPath = "SFX", Name = "SoundEffects"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("SoundEffects", result.Data.GroupName);
        }

        [Test]
        public void MixerGroup_Delete_RemovesGroup()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "Temp" });

            var result = AudioMixerGroupTool.Execute(new AudioMixerGroupParams
            {
                MixerAssetPath = TestMixerPath, Operation = "delete", GroupPath = "Temp"
            });

            Assert.IsTrue(result.Success, result.Error);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(TestMixerPath);
            Assert.AreEqual(0, mixer.FindMatchingGroups("Temp").Length);
        }

        [Test]
        public void MixerGroup_Delete_RefusesMasterGroup()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });

            var result = AudioMixerGroupTool.Execute(new AudioMixerGroupParams
            {
                MixerAssetPath = TestMixerPath, Operation = "delete", GroupPath = "Master"
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void MixerGroup_Move_ReparentsAndLeavesTheOldParent()
        {
            // The exact regression this test exists for: AddChildToParent's re-parenting behavior
            // when the child already has a parent was never runtime-verified during implementation
            // (no live Editor was reachable then) — if it does not detach from the old parent, UI
            // would incorrectly appear under BOTH Master and SFX after the move.
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "UI" });

            var result = AudioMixerGroupTool.Execute(new AudioMixerGroupParams
            {
                MixerAssetPath = TestMixerPath, Operation = "move", GroupPath = "UI", ParentPath = "SFX"
            });

            Assert.IsTrue(result.Success, result.Error);

            var info = AudioMixerInfoTool.Execute(new AudioMixerInfoParams { MixerAssetPath = TestMixerPath });
            Assert.IsTrue(info.Success, info.Error);
            var sfxNode = info.Data.Hierarchy.Children.First(c => c.Name == "SFX");
            Assert.IsTrue(sfxNode.Children != null && sfxNode.Children.Any(c => c.Name == "UI"),
                "UI should now be a child of SFX");
            Assert.IsFalse(info.Data.Hierarchy.Children.Any(c => c.Name == "UI"),
                "UI must no longer be a direct child of Master after being moved under SFX");
        }

        // ── audio/route-source ───────────────────────────────────────────────

        [Test]
        public void RouteSource_RoutesToMatchingGroup()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });
            _testGo = new GameObject("AudioTest_Route");
            _testGo.AddComponent<AudioSource>();

            var result = AudioRouteSourceTool.Execute(new AudioRouteSourceParams
            {
                Name = "AudioTest_Route", MixerAssetPath = TestMixerPath, GroupPath = "SFX"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("SFX", result.Data.GroupName);
            var source = _testGo.GetComponent<AudioSource>();
            Assert.IsNotNull(source.outputAudioMixerGroup);
            Assert.AreEqual("SFX", source.outputAudioMixerGroup.name);
        }

        [Test]
        public void RouteSource_AmbiguousGroupName_Fails()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX", ParentPath = "SFX"
            });
            _testGo = new GameObject("AudioTest_Ambiguous");
            _testGo.AddComponent<AudioSource>();

            var result = AudioRouteSourceTool.Execute(new AudioRouteSourceParams
            {
                Name = "AudioTest_Ambiguous", MixerAssetPath = TestMixerPath, GroupPath = "SFX"
            });

            Assert.IsFalse(result.Success);
        }

        // ── audio/set-source ─────────────────────────────────────────────────

        [Test]
        public void SetSource_UpdatesOnlyProvidedFields()
        {
            _testGo = new GameObject("AudioTest_SetSource");
            var source = _testGo.AddComponent<AudioSource>();
            source.volume = 0.3f;
            source.priority = 200;

            var result = AudioSetSourceTool.Execute(new AudioSetSourceParams
            {
                Name = "AudioTest_SetSource", Mute = true
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.Mute);
            Assert.AreEqual(0.3f, source.volume, 0.001f, "Volume was not provided and must be untouched");
            Assert.AreEqual(200, source.priority, "Priority was not provided and must be untouched");
        }

        [Test]
        public void SetSource_ClampsPanStereoAndReverbZoneMix()
        {
            _testGo = new GameObject("AudioTest_SetSourceClamp");
            _testGo.AddComponent<AudioSource>();

            var result = AudioSetSourceTool.Execute(new AudioSetSourceParams
            {
                Name = "AudioTest_SetSourceClamp", PanStereo = 5f, ReverbZoneMix = -1f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1f, result.Data.PanStereo, 0.001f);
            Assert.AreEqual(0f, result.Data.ReverbZoneMix, 0.001f);
        }

        [Test]
        public void SetSource_NoAudioSource_ReturnsError()
        {
            _testGo = new GameObject("AudioTest_SetSourceNoSource");

            var result = AudioSetSourceTool.Execute(new AudioSetSourceParams { Name = "AudioTest_SetSourceNoSource", Mute = true });

            Assert.IsFalse(result.Success);
        }
    }
}
