#if MOSAIC_HAS_TIMELINE
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mosaic.Bridge.Tools.Timeline;

namespace Mosaic.Bridge.Tests.Unit.Tools.Timeline
{
    [TestFixture]
    [Category("Unit")]
    [Category("Timeline")]
    public class TimelineToolTests
    {
        private const string TestAssetPath = "Assets/TimelineToolTest.playable";

        [TearDown]
        public void Cleanup()
        {
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TestAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(TestAssetPath);
            }
        }

        [Test]
        public void Create_ReturnsOk_WithValidParams()
        {
            var result = TimelineCreateTool.Create(new TimelineCreateParams
            {
                Name = "TestTimeline",
                Path = TestAssetPath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(TestAssetPath, result.Data.AssetPath);
            Assert.AreEqual("TestTimeline", result.Data.Name);

            var loaded = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TestAssetPath);
            Assert.IsNotNull(loaded, "TimelineAsset should exist on disk");
        }

        [Test]
        public void AddTrack_ReturnsOk_AfterCreate()
        {
            // Setup: create a timeline first
            TimelineCreateTool.Create(new TimelineCreateParams
            {
                Name = "TestTimeline",
                Path = TestAssetPath
            });

            var result = TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TestAssetPath,
                TrackType = "Animation",
                Name = "TestAnimTrack"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Animation", result.Data.TrackType);
            Assert.AreEqual(0, result.Data.TrackIndex);
        }

        [Test]
        public void AddTrack_InvalidType_ReturnsFail()
        {
            TimelineCreateTool.Create(new TimelineCreateParams
            {
                Name = "TestTimeline",
                Path = TestAssetPath
            });

            var result = TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TestAssetPath,
                TrackType = "InvalidType"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Info_NonexistentAsset_ReturnsFail()
        {
            var result = TimelineInfoTool.Info(new TimelineInfoParams
            {
                AssetPath = "Assets/nonexistent.playable"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Info_ReturnsTrackData_AfterAddTrack()
        {
            TimelineCreateTool.Create(new TimelineCreateParams
            {
                Name = "TestTimeline",
                Path = TestAssetPath
            });
            TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TestAssetPath,
                TrackType = "Activation",
                Name = "MyActivation"
            });

            var result = TimelineInfoTool.Info(new TimelineInfoParams
            {
                AssetPath = TestAssetPath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.TrackCount);
            Assert.AreEqual("Activation", result.Data.Tracks[0].Type);
        }

        // O4 §4.5 P2: timeline structure + director — Group nesting, the global Marker track,
        // frameRate/durationMode on create, and playOnAwake/wrapMode/updateMode/initialTime on
        // set-director.

        [Test]
        public void Create_WithFrameRateAndFixedDuration_Applies()
        {
            var result = TimelineCreateTool.Create(new TimelineCreateParams
            {
                Name = "TestTimeline", Path = TestAssetPath,
                FrameRate = 30, DurationMode = "FixedLength", FixedDuration = 12.5,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(30, result.Data.FrameRate, 0.0001);
            Assert.AreEqual("FixedLength", result.Data.DurationMode);
            Assert.AreEqual(12.5, result.Data.FixedDuration, 0.0001);
        }

        [Test]
        public void AddTrack_Group_NestsAChildTrackUnderIt()
        {
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = TestAssetPath });
            TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TestAssetPath, TrackType = "Group", Name = "MyGroup",
            });

            var result = TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TestAssetPath, TrackType = "Audio", Name = "ChildAudio",
                ParentGroupName = "MyGroup",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("MyGroup", result.Data.ParentGroupName);

            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TestAssetPath);
            var group = timeline.GetRootTracks().Single();
            Assert.AreEqual("ChildAudio", group.GetChildTracks().Single().name);
        }

        [Test]
        public void AddTrack_ParentGroupNotFound_ReturnsNotFound()
        {
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = TestAssetPath });

            var result = TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TestAssetPath, TrackType = "Audio", ParentGroupName = "NoSuchGroup",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void AddTrack_Marker_CreatesTheGlobalMarkerTrackIdempotently()
        {
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = TestAssetPath });

            var first = TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TestAssetPath, TrackType = "Marker",
            });
            var second = TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TestAssetPath, TrackType = "Marker",
            });

            Assert.IsTrue(first.Success, first.Error);
            Assert.IsTrue(second.Success, second.Error);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TestAssetPath);
            Assert.IsNotNull(timeline.markerTrack);
        }

        [Test]
        public void AddTrack_Mute_Applies()
        {
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = TestAssetPath });

            var result = TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TestAssetPath, TrackType = "Audio", Mute = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.Muted);
        }

        [Test]
        public void SetDirector_AppliesPlaybackSettings()
        {
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = TestAssetPath });
            var go = new GameObject("TimelineToolTestDirector");
            try
            {
                var result = TimelineSetDirectorTool.SetDirector(new TimelineSetDirectorParams
                {
                    InstanceId = go.GetInstanceID(), TimelineAssetPath = TestAssetPath,
                    PlayOnAwake = false, WrapMode = "Loop", UpdateMode = "UnscaledGameTime", InitialTime = 1.5,
                });

                Assert.IsTrue(result.Success, result.Error);
                Assert.IsFalse(result.Data.PlayOnAwake);
                Assert.AreEqual("Loop", result.Data.WrapMode);
                Assert.AreEqual("UnscaledGameTime", result.Data.UpdateMode);
                Assert.AreEqual(1.5, result.Data.InitialTime, 0.0001);

                var director = go.GetComponent<PlayableDirector>();
                Assert.AreEqual(DirectorWrapMode.Loop, director.extrapolationMode);
                Assert.AreEqual(DirectorUpdateMode.UnscaledGameTime, director.timeUpdateMode);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
