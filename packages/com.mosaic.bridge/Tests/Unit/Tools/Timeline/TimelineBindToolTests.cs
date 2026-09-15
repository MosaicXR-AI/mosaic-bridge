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
    // O4 §3.3 / §4.5 P2: timeline/bind used to take only InstanceIds, which is why the agent fell
    // back to a run-block for anything named in a build plan. Covers bind-by-path, auto-picking the
    // component a track's TrackBindingTypeAttribute wants, and set-reference for
    // ControlPlayableAsset.sourceGameObject (the KB's #1 reported cause of an inert timeline).
    [TestFixture]
    [Category("Unit")]
    [Category("Timeline")]
    public class TimelineBindToolTests
    {
        private const string TimelinePath = "Assets/TimelineBindToolTest.playable";
        private const string PrefabPath = "Assets/TimelineBindToolTest.prefab";

        private GameObject _directorGo;
        private GameObject _targetGo;
        private PlayableDirector _director;
        private TimelineAsset _timeline;

        [SetUp]
        public void SetUp()
        {
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = TimelinePath });
            _timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);

            _directorGo = new GameObject("TimelineBindToolTestDirector");
            _director = _directorGo.AddComponent<PlayableDirector>();
            _director.playableAsset = _timeline;

            _targetGo = new GameObject("TimelineBindToolTestTarget");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_directorGo);
            Object.DestroyImmediate(_targetGo);
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
                AssetDatabase.DeleteAsset(TimelinePath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);
        }

        private int AddTrack(string trackType)
        {
            TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams
            {
                AssetPath = TimelinePath, TrackType = trackType, Name = trackType,
            });
            return _timeline.GetOutputTracks().Count() - 1;
        }

        [Test]
        public void Bind_ByInstanceId_Works()
        {
            var trackIndex = AddTrack("Activation");

            var result = TimelineBindTool.Bind(new TimelineBindParams
            {
                DirectorInstanceId = _directorGo.GetInstanceID(), TrackIndex = trackIndex,
                TargetInstanceId = _targetGo.GetInstanceID(),
            });

            Assert.IsTrue(result.Success, result.Error);
            var track = _timeline.GetOutputTracks().ElementAt(trackIndex);
            Assert.AreEqual(_targetGo, _director.GetGenericBinding(track));
        }

        [Test]
        public void Bind_ByPath_Works()
        {
            var trackIndex = AddTrack("Activation");

            var result = TimelineBindTool.Bind(new TimelineBindParams
            {
                DirectorInstanceId = _directorGo.GetInstanceID(), TrackIndex = trackIndex,
                TargetPath = "TimelineBindToolTestTarget",
            });

            Assert.IsTrue(result.Success, result.Error);
            var track = _timeline.GetOutputTracks().ElementAt(trackIndex);
            Assert.AreEqual(_targetGo, _director.GetGenericBinding(track));
        }

        [Test]
        public void Bind_AutoPicksComponent_ForAnimationTrack()
        {
            var trackIndex = AddTrack("Animation");
            var animator = _targetGo.AddComponent<Animator>();

            var result = TimelineBindTool.Bind(new TimelineBindParams
            {
                DirectorInstanceId = _directorGo.GetInstanceID(), TrackIndex = trackIndex,
                TargetInstanceId = _targetGo.GetInstanceID(),
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Animator", result.Data.BoundComponentType);
            var track = _timeline.GetOutputTracks().ElementAt(trackIndex);
            Assert.AreEqual(animator, _director.GetGenericBinding(track));
        }

        [Test]
        public void Bind_AutoPick_MissingComponent_ReturnsNotFound()
        {
            var trackIndex = AddTrack("Animation"); // wants an Animator; _targetGo has none

            var result = TimelineBindTool.Bind(new TimelineBindParams
            {
                DirectorInstanceId = _directorGo.GetInstanceID(), TrackIndex = trackIndex,
                TargetInstanceId = _targetGo.GetInstanceID(),
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
            StringAssert.Contains("Animator", result.Error);
        }

        [Test]
        public void Bind_TargetNotFound_ReturnsNotFound()
        {
            var trackIndex = AddTrack("Activation");

            var result = TimelineBindTool.Bind(new TimelineBindParams
            {
                DirectorInstanceId = _directorGo.GetInstanceID(), TrackIndex = trackIndex,
                TargetPath = "NoSuchObjectAnywhere",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Bind_UnknownAction_ReturnsInvalidParam()
        {
            var trackIndex = AddTrack("Activation");

            var result = TimelineBindTool.Bind(new TimelineBindParams
            {
                Action = "bogus", DirectorInstanceId = _directorGo.GetInstanceID(), TrackIndex = trackIndex,
                TargetInstanceId = _targetGo.GetInstanceID(),
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetReference_ControlTrack_SetsSourceGameObject()
        {
            var trackIndex = AddTrack("Control");
            var prefabGo = new GameObject("TimelineBindToolTestPrefab");
            try
            {
                PrefabUtility.SaveAsPrefabAsset(prefabGo, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(prefabGo);
            }
            TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = trackIndex, ClipAssetPath = PrefabPath,
            });

            var result = TimelineBindTool.Bind(new TimelineBindParams
            {
                Action = "set-reference", DirectorInstanceId = _directorGo.GetInstanceID(), TrackIndex = trackIndex,
                ClipIndex = 0, ReferenceInstanceId = _targetGo.GetInstanceID(),
            });

            Assert.IsTrue(result.Success, result.Error);
            var track = _timeline.GetOutputTracks().ElementAt(trackIndex);
            var clip = track.GetClips().Single();
            var controlAsset = (ControlPlayableAsset)clip.asset;
            var value = _director.GetReferenceValue(controlAsset.sourceGameObject.exposedName, out var idValid);
            Assert.IsTrue(idValid);
            Assert.AreEqual(_targetGo, value);
        }

        [Test]
        public void SetReference_UnsupportedAssetType_ReturnsInvalidParam()
        {
            var trackIndex = AddTrack("Animation");
            TimelineAddClipTool.AddClip(new TimelineAddClipParams
            {
                AssetPath = TimelinePath, TrackIndex = trackIndex,
            });

            var result = TimelineBindTool.Bind(new TimelineBindParams
            {
                Action = "set-reference", DirectorInstanceId = _directorGo.GetInstanceID(), TrackIndex = trackIndex,
                ClipIndex = 0, ReferenceInstanceId = _targetGo.GetInstanceID(),
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetReference_ClipIndexOutOfRange_ReturnsOutOfRange()
        {
            var trackIndex = AddTrack("Control");

            var result = TimelineBindTool.Bind(new TimelineBindParams
            {
                Action = "set-reference", DirectorInstanceId = _directorGo.GetInstanceID(), TrackIndex = trackIndex,
                ClipIndex = 0, ReferenceInstanceId = _targetGo.GetInstanceID(),
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
        }
    }
}
#endif
