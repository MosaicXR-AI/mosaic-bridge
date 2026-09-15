#if MOSAIC_HAS_TIMELINE
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Timeline;
using Mosaic.Bridge.Tools.Timeline;

namespace Mosaic.Bridge.Tests.Unit.Tools.Timeline
{
    // O4 §4.5 P2: timeline/remove — deleting a track/clip/marker used to require dropping to a
    // run-block since the API had no delete route at all.
    [TestFixture]
    [Category("Unit")]
    [Category("Timeline")]
    public class TimelineRemoveToolTests
    {
        private const string TimelinePath = "Assets/TimelineRemoveToolTest.playable";
        private const string SignalPath = "Assets/TimelineRemoveToolTest.signal";

        [SetUp]
        public void SetUp() =>
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = TimelinePath });

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
                AssetDatabase.DeleteAsset(TimelinePath);
            if (AssetDatabase.LoadAssetAtPath<SignalAsset>(SignalPath) != null)
                AssetDatabase.DeleteAsset(SignalPath);
        }

        [Test]
        public void RemoveTrack_DeletesIt()
        {
            TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams { AssetPath = TimelinePath, TrackType = "Audio", Name = "A" });

            var result = TimelineRemoveTool.Remove(new TimelineRemoveParams
            {
                AssetPath = TimelinePath, Target = "track", TrackIndex = 0,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("A", result.Data.RemovedName);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            Assert.AreEqual(0, timeline.GetOutputTracks().Count());
        }

        [Test]
        public void RemoveTrack_OutOfRange_ReturnsFail()
        {
            var result = TimelineRemoveTool.Remove(new TimelineRemoveParams
            {
                AssetPath = TimelinePath, Target = "track", TrackIndex = 0,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
        }

        [Test]
        public void RemoveClip_DeletesIt()
        {
            TimelineAddTrackTool.AddTrack(new TimelineAddTrackParams { AssetPath = TimelinePath, TrackType = "Activation", Name = "A" });
            TimelineAddClipTool.AddClip(new TimelineAddClipParams { AssetPath = TimelinePath, TrackIndex = 0 });

            var result = TimelineRemoveTool.Remove(new TimelineRemoveParams
            {
                AssetPath = TimelinePath, Target = "clip", TrackIndex = 0, ClipIndex = 0,
            });

            Assert.IsTrue(result.Success, result.Error);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            Assert.AreEqual(0, timeline.GetOutputTracks().First().GetClips().Count());
        }

        [Test]
        public void RemoveMarker_OnGlobalMarkerTrack_DeletesIt()
        {
            TimelineSignalTool.Execute(new TimelineSignalParams { Action = "create-asset", AssetPath = SignalPath });
            TimelineSignalTool.Execute(new TimelineSignalParams
            {
                Action = "add-emitter", TimelineAssetPath = TimelinePath, SignalAssetPath = SignalPath, Time = 1,
            });

            var result = TimelineRemoveTool.Remove(new TimelineRemoveParams
            {
                AssetPath = TimelinePath, Target = "marker", MarkerIndex = 0,
            });

            Assert.IsTrue(result.Success, result.Error);
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            Assert.AreEqual(0, timeline.markerTrack.GetMarkers().Count());
        }

        [Test]
        public void RemoveMarker_NoGlobalMarkerTrack_ReturnsNotFound()
        {
            var result = TimelineRemoveTool.Remove(new TimelineRemoveParams
            {
                AssetPath = TimelinePath, Target = "marker", MarkerIndex = 0,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Remove_UnknownTarget_ReturnsInvalidParam()
        {
            var result = TimelineRemoveTool.Remove(new TimelineRemoveParams
            {
                AssetPath = TimelinePath, Target = "bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
#endif
