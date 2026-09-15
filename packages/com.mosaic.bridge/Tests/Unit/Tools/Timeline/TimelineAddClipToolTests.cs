#if MOSAIC_HAS_TIMELINE
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Mosaic.Bridge.Tools.Timeline;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Unit.Tools.Timeline
{
    /// <summary>
    /// L3: timeline/add-clip used to ignore ClipAssetPath for every track type except
    /// AnimationTrack — an Audio clip was created empty (plays nothing) and a Control clip got no
    /// prefab, with no error explaining why. These tests cover the fixed wiring plus the new
    /// loud failure when ClipAssetPath's asset type doesn't match what the track needs.
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("Timeline")]
    public class TimelineAddClipToolTests
    {
        private const string TimelinePath = "Assets/TimelineAddClipToolTest.playable";
        private const string AudioClipPath = "Assets/TimelineAddClipToolTest.wav";
        private const string PrefabPath = "Assets/TimelineAddClipToolTest.prefab";
        private const string AnimClipPath = "Assets/TimelineAddClipToolTest.anim";

        [TearDown]
        public void Cleanup()
        {
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
                AssetDatabase.DeleteAsset(TimelinePath);
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(AudioClipPath) != null)
                AssetDatabase.DeleteAsset(AudioClipPath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimClipPath) != null)
                AssetDatabase.DeleteAsset(AnimClipPath);
        }

        private static void CreateTwoSecondAnimClip(string path)
        {
            AnimationClipTool.Execute(new AnimationClipParams { Action = "create", Path = path });
            AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "set-curve", Path = path, PropertyPath = "", ComponentType = "Transform",
                PropertyName = "localPosition.x",
                KeyframeTimes = new float[] { 0f, 2f }, KeyframeValues = new float[] { 0f, 1f },
            });
        }

        private static void CreateTimelineWithTrack(string trackType)
        {
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = TimelinePath });
            TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TimelinePath, TrackType = trackType, Name = "Track"
            });
        }

        private static void CreateSilentWavAsset(string path)
        {
            // A minimal but valid WAV file — 100 samples of 16-bit mono silence at 44.1kHz.
            // FMOD (Unity's audio importer) rejects a zero-length data chunk outright, so this
            // needs real (if silent) sample data, not just a header.
            const int sampleCount = 100;
            const int dataBytes = sampleCount * 2; // 16-bit mono
            var bytes = new byte[44 + dataBytes];
            void WriteAscii(int offset, string s) { for (int i = 0; i < s.Length; i++) bytes[offset + i] = (byte)s[i]; }
            void WriteU32(int offset, uint v) { bytes[offset] = (byte)v; bytes[offset+1] = (byte)(v>>8); bytes[offset+2] = (byte)(v>>16); bytes[offset+3] = (byte)(v>>24); }
            void WriteU16(int offset, ushort v) { bytes[offset] = (byte)v; bytes[offset+1] = (byte)(v>>8); }

            WriteAscii(0, "RIFF");
            WriteU32(4, (uint)(36 + dataBytes));
            WriteAscii(8, "WAVE");
            WriteAscii(12, "fmt ");
            WriteU32(16, 16);
            WriteU16(20, 1);      // PCM
            WriteU16(22, 1);      // mono
            WriteU32(24, 44100);  // sample rate
            WriteU32(28, 44100 * 2); // byte rate
            WriteU16(32, 2);      // block align
            WriteU16(34, 16);     // bits per sample
            WriteAscii(36, "data");
            WriteU32(40, dataBytes);
            // sample bytes already zero-initialized (silence)

            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), path), bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void AudioTrack_WithClipAssetPath_WiresTheRealAudioClip()
        {
            CreateTimelineWithTrack("Audio");
            CreateSilentWavAsset(AudioClipPath);
            var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioClipPath);
            Assert.IsNotNull(audioClip, "test setup: WAV must import as an AudioClip");

            var result = TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = 0, ClipAssetPath = AudioClipPath
            });

            Assert.IsTrue(result.Success, result.Error);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            var track = (AudioTrack)System.Linq.Enumerable.First(timeline.GetOutputTracks());
            var clip = System.Linq.Enumerable.Single(track.GetClips());
            Assert.AreEqual(audioClip, ((AudioPlayableAsset)clip.asset).clip,
                "the clip must play the real AudioClip, not an empty default");
        }

        [Test]
        public void ControlTrack_WithClipAssetPath_WiresThePrefab()
        {
            CreateTimelineWithTrack("Control");
            var prefabGo = new GameObject("TimelineAddClipToolTestPrefab");
            try
            {
                PrefabUtility.SaveAsPrefabAsset(prefabGo, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(prefabGo);
            }
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefabAsset, "test setup: prefab must exist");

            var result = TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = 0, ClipAssetPath = PrefabPath
            });

            Assert.IsTrue(result.Success, result.Error);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            var track = (ControlTrack)System.Linq.Enumerable.First(timeline.GetOutputTracks());
            var clip = System.Linq.Enumerable.Single(track.GetClips());
            Assert.AreEqual(prefabAsset, ((ControlPlayableAsset)clip.asset).prefabGameObject,
                "the Control clip must reference the real prefab, not be left empty");
        }

        [Test]
        public void AudioTrack_WithMismatchedAssetType_FailsRatherThanCreatingAnEmptyClip()
        {
            CreateTimelineWithTrack("Audio");
            var prefabGo = new GameObject("TimelineAddClipToolTestPrefab2");
            try
            {
                PrefabUtility.SaveAsPrefabAsset(prefabGo, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(prefabGo);
            }

            var result = TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = 0, ClipAssetPath = PrefabPath
            });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("AudioClip", result.Error);

            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            var track = (AudioTrack)System.Linq.Enumerable.First(timeline.GetOutputTracks());
            Assert.AreEqual(0, System.Linq.Enumerable.Count(track.GetClips()),
                "no orphan clip should be left on the track after a rejected ClipAssetPath");
        }

        [Test]
        public void ActivationTrack_WithClipAssetPath_FailsWithUnsupportedTrackMessage()
        {
            CreateTimelineWithTrack("Activation");
            CreateSilentWavAsset(AudioClipPath);

            var result = TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = 0, ClipAssetPath = AudioClipPath
            });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("not supported", result.Error);
        }

        // O4 §4.5 P2: typed clips — TimelineClip.* fields on any track, plus per-track
        // AnimationPlayableAsset/AudioPlayableAsset/ControlPlayableAsset fields.

        [Test]
        public void TimelineClipFields_ApplyAndRoundTrip()
        {
            CreateTimelineWithTrack("Animation");
            CreateTwoSecondAnimClip(AnimClipPath);

            var result = TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = 0, ClipAssetPath = AnimClipPath, Start = 0, Duration = 1,
                DisplayName = "MyClip", ClipIn = 0.5, TimeScale = 2.0,
                EaseInDuration = 0.1, EaseOutDuration = 0.2,
                BlendInDuration = 0.3, BlendOutDuration = 0.4,
                BlendInCurveMode = "Manual", BlendOutCurveMode = "Auto",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("MyClip", result.Data.ClipName);
            Assert.AreEqual(0.5, result.Data.ClipIn);
            Assert.AreEqual(2.0, result.Data.TimeScale);
            Assert.AreEqual(0.1, result.Data.EaseInDuration);
            Assert.AreEqual(0.2, result.Data.EaseOutDuration);
            Assert.AreEqual(0.3, result.Data.BlendInDuration);
            Assert.AreEqual(0.4, result.Data.BlendOutDuration);
            Assert.AreEqual("Manual", result.Data.BlendInCurveMode);
            Assert.AreEqual("Auto", result.Data.BlendOutCurveMode);
        }

        [Test]
        public void UnknownBlendInCurveMode_ReturnsFail()
        {
            CreateTimelineWithTrack("Animation");

            var result = TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = 0, BlendInCurveMode = "Bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void AnimationTrack_TypedFields_Apply()
        {
            CreateTimelineWithTrack("Animation");
            CreateTwoSecondAnimClip(AnimClipPath);

            var result = TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = 0, ClipAssetPath = AnimClipPath,
                AnimPosition = new[] { 1f, 2f, 3f },
                AnimEulerAngles = new[] { 0f, 90f, 0f },
                AnimLoop = "On",
                AnimRemoveStartOffset = true,
                AnimApplyFootIK = false,
            });

            Assert.IsTrue(result.Success, result.Error);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            var track = (AnimationTrack)System.Linq.Enumerable.First(timeline.GetOutputTracks());
            var clip = System.Linq.Enumerable.Single(track.GetClips());
            var asset = (AnimationPlayableAsset)clip.asset;

            Assert.AreEqual(new Vector3(1, 2, 3), asset.position);
            Assert.AreEqual(new Vector3(0, 90, 0), asset.eulerAngles);
            Assert.AreEqual(AnimationPlayableAsset.LoopMode.On, asset.loop);
            Assert.IsTrue(asset.removeStartOffset);
            Assert.IsFalse(asset.applyFootIK);
        }

        [Test]
        public void AudioTrack_AudioLoop_Applies()
        {
            CreateTimelineWithTrack("Audio");
            CreateSilentWavAsset(AudioClipPath);

            var result = TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = 0, ClipAssetPath = AudioClipPath, AudioLoop = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            var track = (AudioTrack)System.Linq.Enumerable.First(timeline.GetOutputTracks());
            var clip = System.Linq.Enumerable.Single(track.GetClips());
            Assert.IsTrue(((AudioPlayableAsset)clip.asset).loop);
        }

        [Test]
        public void ControlTrack_TypedFields_Apply()
        {
            CreateTimelineWithTrack("Control");
            var prefabGo = new GameObject("TimelineAddClipToolTestPrefab3");
            try
            {
                PrefabUtility.SaveAsPrefabAsset(prefabGo, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(prefabGo);
            }

            var result = TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = 0, ClipAssetPath = PrefabPath,
                ControlUpdateParticle = true, ControlPostPlayback = "Revert",
            });

            Assert.IsTrue(result.Success, result.Error);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            var track = (ControlTrack)System.Linq.Enumerable.First(timeline.GetOutputTracks());
            var clip = System.Linq.Enumerable.Single(track.GetClips());
            var asset = (ControlPlayableAsset)clip.asset;
            Assert.IsTrue(asset.updateParticle);
            Assert.AreEqual(ActivationControlPlayable.PostPlaybackState.Revert, asset.postPlayback);
        }
    }
}
#endif
