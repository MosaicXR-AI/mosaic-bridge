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

        // ── audio/mixer-expose-param ─────────────────────────────────────────

        [Test]
        public void ExposeParam_Volume_IsReadableViaPublicGetFloat()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });

            var result = AudioMixerExposeParamTool.Execute(new AudioMixerExposeParamParams
            {
                MixerAssetPath = TestMixerPath, Operation = "expose",
                GroupPath = "SFX", ParamKind = "volume", Name = "SFXVolume",
            });

            Assert.IsTrue(result.Success, result.Error);

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(TestMixerPath);
            Assert.IsTrue(mixer.GetFloat("SFXVolume", out float value));
            Assert.AreEqual(0f, value, 0.001f);
        }

        [Test]
        public void ExposeParam_UnknownKind_ReturnsInvalidParam()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });

            var result = AudioMixerExposeParamTool.Execute(new AudioMixerExposeParamParams
            {
                MixerAssetPath = TestMixerPath, Operation = "expose",
                GroupPath = "SFX", ParamKind = "loudness", Name = "SFXVolume",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ExposeParam_ListAfterExpose_ContainsName()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });
            AudioMixerExposeParamTool.Execute(new AudioMixerExposeParamParams
            {
                MixerAssetPath = TestMixerPath, Operation = "expose",
                GroupPath = "SFX", ParamKind = "volume", Name = "SFXVolume",
            });

            var result = AudioMixerExposeParamTool.Execute(new AudioMixerExposeParamParams
            {
                MixerAssetPath = TestMixerPath, Operation = "list",
            });

            Assert.IsTrue(result.Success, result.Error);
            CollectionAssert.Contains(result.Data.ExposedParameterNames, "SFXVolume");
        }

        [Test]
        public void ExposeParam_Rename_UpdatesNameAndOldNameStopsWorking()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });
            AudioMixerExposeParamTool.Execute(new AudioMixerExposeParamParams
            {
                MixerAssetPath = TestMixerPath, Operation = "expose",
                GroupPath = "SFX", ParamKind = "volume", Name = "SFXVolume",
            });

            var result = AudioMixerExposeParamTool.Execute(new AudioMixerExposeParamParams
            {
                MixerAssetPath = TestMixerPath, Operation = "rename", Name = "SFXVolume", NewName = "EffectsVolume",
            });

            Assert.IsTrue(result.Success, result.Error);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(TestMixerPath);
            Assert.IsTrue(mixer.GetFloat("EffectsVolume", out _));
            Assert.IsFalse(mixer.GetFloat("SFXVolume", out _));
        }

        [Test]
        public void ExposeParam_Remove_ClearsIt()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });
            AudioMixerExposeParamTool.Execute(new AudioMixerExposeParamParams
            {
                MixerAssetPath = TestMixerPath, Operation = "expose",
                GroupPath = "SFX", ParamKind = "volume", Name = "SFXVolume",
            });

            var result = AudioMixerExposeParamTool.Execute(new AudioMixerExposeParamParams
            {
                MixerAssetPath = TestMixerPath, Operation = "remove", Name = "SFXVolume",
            });

            Assert.IsTrue(result.Success, result.Error);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(TestMixerPath);
            Assert.IsFalse(mixer.GetFloat("SFXVolume", out _));
        }

        [Test]
        public void ExposeParam_RenameUnknown_ReturnsNotFound()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });

            var result = AudioMixerExposeParamTool.Execute(new AudioMixerExposeParamParams
            {
                MixerAssetPath = TestMixerPath, Operation = "rename", Name = "DoesNotExist", NewName = "Whatever",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // ── audio/mixer-snapshot ─────────────────────────────────────────────

        [Test]
        public void Snapshot_Add_CreatesWithDefaultValues()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });

            var result = AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Paused",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Paused", result.Data.SnapshotName);
        }

        [Test]
        public void Snapshot_List_ContainsDefaultAndNewSnapshot()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Paused",
            });

            var result = AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "list",
            });

            Assert.IsTrue(result.Success, result.Error);
            CollectionAssert.Contains(result.Data.SnapshotNames, "Paused");
        }

        [Test]
        public void Snapshot_Rename_UpdatesName()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Paused",
            });

            var result = AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "rename", Name = "Paused", NewName = "Underwater",
            });

            Assert.IsTrue(result.Success, result.Error);
            var list = AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "list",
            });
            CollectionAssert.Contains(list.Data.SnapshotNames, "Underwater");
            CollectionAssert.DoesNotContain(list.Data.SnapshotNames, "Paused");
        }

        [Test]
        public void Snapshot_SetTarget_Succeeds()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Paused",
            });

            var result = AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "set-target", Name = "Paused",
            });

            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void Snapshot_RenameUnknown_ReturnsNotFound()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });

            var result = AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "rename", Name = "DoesNotExist", NewName = "Whatever",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // ── audio/mixer-set-value ────────────────────────────────────────────

        [Test]
        public void SetValue_SetThenGet_RoundTrips()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "Music" });
            AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Paused",
            });

            var setResult = AudioMixerSetValueTool.Execute(new AudioMixerSetValueParams
            {
                MixerAssetPath = TestMixerPath, SnapshotName = "Paused", GroupPath = "Music",
                ParamKind = "volume", Operation = "set", Value = -12f,
            });
            Assert.IsTrue(setResult.Success, setResult.Error);

            var getResult = AudioMixerSetValueTool.Execute(new AudioMixerSetValueParams
            {
                MixerAssetPath = TestMixerPath, SnapshotName = "Paused", GroupPath = "Music",
                ParamKind = "volume", Operation = "get",
            });

            Assert.IsTrue(getResult.Success, getResult.Error);
            Assert.AreEqual(-12f, getResult.Data.Value, 0.01f);
        }

        [Test]
        public void SetValue_UnknownParamKind_ReturnsInvalidParam()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "Music" });
            AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Paused",
            });

            var result = AudioMixerSetValueTool.Execute(new AudioMixerSetValueParams
            {
                MixerAssetPath = TestMixerPath, SnapshotName = "Paused", GroupPath = "Music",
                ParamKind = "loudness", Value = -12f,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetValue_UnknownSnapshot_ReturnsNotFound()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "Music" });

            var result = AudioMixerSetValueTool.Execute(new AudioMixerSetValueParams
            {
                MixerAssetPath = TestMixerPath, SnapshotName = "DoesNotExist", GroupPath = "Music",
                ParamKind = "volume", Value = -12f,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // ── audio/mixer-transition ───────────────────────────────────────────

        [Test]
        public void Transition_SingleSnapshot_Succeeds()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Paused",
            });

            var result = AudioMixerTransitionTool.Execute(new AudioMixerTransitionParams
            {
                MixerAssetPath = TestMixerPath, SnapshotNames = new[] { "Paused" }, TimeToReach = 0.5f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.5f, result.Data.TimeToReach, 0.001f);
        }

        [Test]
        public void Transition_MultipleSnapshots_BlendsWithUniformWeights()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Paused",
            });
            AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Underwater",
            });

            var result = AudioMixerTransitionTool.Execute(new AudioMixerTransitionParams
            {
                MixerAssetPath = TestMixerPath, SnapshotNames = new[] { "Paused", "Underwater" },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.5f, result.Data.Weights[0], 0.001f);
            Assert.AreEqual(0.5f, result.Data.Weights[1], 0.001f);
        }

        [Test]
        public void Transition_UnknownSnapshot_ReturnsNotFound()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });

            var result = AudioMixerTransitionTool.Execute(new AudioMixerTransitionParams
            {
                MixerAssetPath = TestMixerPath, SnapshotNames = new[] { "DoesNotExist" },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Transition_MismatchedWeightsLength_ReturnsInvalidParam()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Paused",
            });

            var result = AudioMixerTransitionTool.Execute(new AudioMixerTransitionParams
            {
                MixerAssetPath = TestMixerPath, SnapshotNames = new[] { "Paused" }, Weights = new[] { 0.5f, 0.5f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── audio/mixer-effect ───────────────────────────────────────────────

        [Test]
        public void Effect_ListTypes_ReturnsNonEmptyRegistry()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });

            var result = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, Operation = "list-types",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotEmpty(result.Data.AvailableEffectTypes);
        }

        [Test]
        public void Effect_Add_AppearsInList()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });

            var types = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "list-types",
            });
            var effectType = types.Data.AvailableEffectTypes.First(t => t.IndexOf("Lowpass", System.StringComparison.OrdinalIgnoreCase) >= 0
                || t.IndexOf("low", System.StringComparison.OrdinalIgnoreCase) >= 0);

            var result = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "add", EffectType = effectType,
            });

            Assert.IsTrue(result.Success, result.Error);

            var list = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "list",
            });
            Assert.IsTrue(list.Success, list.Error);
            Assert.AreEqual(result.Data.EffectIndex + 1, list.Data.EffectNames.Length);
        }

        [Test]
        public void Effect_AddUnknownType_ReturnsInvalidParam()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });

            var result = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "add",
                EffectType = "Not A Real Effect Mosaic",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Effect_Remove_ShrinksList()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });
            var types = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "list-types",
            });
            var added = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "add",
                EffectType = types.Data.AvailableEffectTypes[0],
            });
            var before = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "list",
            });

            var result = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "remove",
                EffectIndex = added.Data.EffectIndex,
            });

            Assert.IsTrue(result.Success, result.Error);
            var after = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "list",
            });
            Assert.AreEqual(before.Data.EffectNames.Length - 1, after.Data.EffectNames.Length);
        }

        [Test]
        public void Effect_SetThenGetMixLevel_RoundTrips()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });
            AudioMixerSnapshotTool.Execute(new AudioMixerSnapshotParams
            {
                MixerAssetPath = TestMixerPath, Operation = "add", Name = "Paused",
            });
            var types = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "list-types",
            });
            var added = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "add",
                EffectType = types.Data.AvailableEffectTypes[0],
            });

            var setResult = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "set-value",
                EffectIndex = added.Data.EffectIndex, SnapshotName = "Paused", Value = 0.5f,
            });
            Assert.IsTrue(setResult.Success, setResult.Error);

            var getResult = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "get-value",
                EffectIndex = added.Data.EffectIndex, SnapshotName = "Paused",
            });

            Assert.IsTrue(getResult.Success, getResult.Error);
            Assert.AreEqual(0.5f, getResult.Data.Value, 0.01f);
        }

        [Test]
        public void Effect_RemoveOutOfRange_ReturnsOutOfRange()
        {
            AudioCreateMixerTool.Execute(new AudioCreateMixerParams { AssetPath = TestMixerPath });
            AudioMixerGroupTool.Execute(new AudioMixerGroupParams { MixerAssetPath = TestMixerPath, Operation = "add", Name = "SFX" });

            var result = AudioMixerEffectTool.Execute(new AudioMixerEffectParams
            {
                MixerAssetPath = TestMixerPath, GroupPath = "SFX", Operation = "remove", EffectIndex = 99,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
        }
    }
}
