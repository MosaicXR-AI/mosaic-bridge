#if MOSAIC_HAS_TIMELINE
using NUnit.Framework;
using UnityEditor;
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
    }
}
#endif
