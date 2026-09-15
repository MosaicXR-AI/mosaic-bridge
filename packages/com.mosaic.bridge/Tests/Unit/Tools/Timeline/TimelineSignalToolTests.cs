#if MOSAIC_HAS_TIMELINE
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Mosaic.Bridge.Tools.Timeline;

namespace Mosaic.Bridge.Tests.Unit.Tools.Timeline
{
    // O4 §4.5 P2: Signals — "fire gameplay event at t=3.2s" is the KB's sanctioned path, but an
    // unwired Signal track is inert without add-emitter + add-receiver.
    [TestFixture]
    [Category("Unit")]
    [Category("Timeline")]
    public class TimelineSignalToolTests
    {
        private const string TimelinePath = "Assets/TimelineSignalToolTest.playable";
        private const string SignalPath = "Assets/TimelineSignalToolTest.signal";

        private GameObject _receiverGo;
        private GameObject _targetGo;

        [SetUp]
        public void SetUp()
        {
            _receiverGo = new GameObject("TimelineSignalToolTestReceiver");
            _targetGo = new GameObject("TimelineSignalToolTestTarget");
            _targetGo.AddComponent<TimelineSignalTestBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_receiverGo);
            Object.DestroyImmediate(_targetGo);
            if (AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null)
                AssetDatabase.DeleteAsset(TimelinePath);
            if (AssetDatabase.LoadAssetAtPath<SignalAsset>(SignalPath) != null)
                AssetDatabase.DeleteAsset(SignalPath);
        }

        [Test]
        public void CreateAsset_CreatesASignalAsset()
        {
            var result = TimelineSignalTool.Execute(new TimelineSignalParams
            {
                Action = "create-asset", AssetPath = SignalPath,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SignalAsset>(SignalPath));
        }

        [Test]
        public void AddEmitter_OnGlobalMarkerTrack_CreatesTheMarker()
        {
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = TimelinePath });
            TimelineSignalTool.Execute(new TimelineSignalParams { Action = "create-asset", AssetPath = SignalPath });

            var result = TimelineSignalTool.Execute(new TimelineSignalParams
            {
                Action = "add-emitter", TimelineAssetPath = TimelinePath, SignalAssetPath = SignalPath,
                Time = 3.2, Retroactive = true, EmitOnce = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3.2, result.Data.Time, 0.0001);
            Assert.IsTrue(result.Data.Retroactive);
            Assert.IsTrue(result.Data.EmitOnce);

            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            Assert.IsNotNull(timeline.markerTrack);
            var emitter = (SignalEmitter)timeline.markerTrack.GetMarkers().Single();
            Assert.AreEqual(3.2, emitter.time, 0.0001);
        }

        [Test]
        public void AddEmitter_MissingSignalAsset_ReturnsNotFound()
        {
            TimelineCreateTool.Create(new TimelineCreateParams { Name = "T", Path = TimelinePath });

            var result = TimelineSignalTool.Execute(new TimelineSignalParams
            {
                Action = "add-emitter", TimelineAssetPath = TimelinePath,
                SignalAssetPath = "Assets/NoSuchSignal.signal", Time = 1,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void AddReceiver_RegistersAPersistentReaction()
        {
            TimelineSignalTool.Execute(new TimelineSignalParams { Action = "create-asset", AssetPath = SignalPath });
            var signalAsset = AssetDatabase.LoadAssetAtPath<SignalAsset>(SignalPath);

            var result = TimelineSignalTool.Execute(new TimelineSignalParams
            {
                Action = "add-receiver",
                ReceiverInstanceId = _receiverGo.GetInstanceID(),
                SignalAssetPath = SignalPath,
                TargetInstanceId = _targetGo.GetInstanceID(),
                TargetComponentType = nameof(TimelineSignalTestBehaviour),
                MethodName = nameof(TimelineSignalTestBehaviour.OnSignal),
            });

            Assert.IsTrue(result.Success, result.Error);
            var receiver = _receiverGo.GetComponent<SignalReceiver>();
            Assert.IsNotNull(receiver);
            var reaction = receiver.GetReaction(signalAsset);
            Assert.IsNotNull(reaction);
            Assert.AreEqual(1, reaction.GetPersistentEventCount());
        }

        [Test]
        public void AddReceiver_DuplicateSignal_ReturnsConflict()
        {
            TimelineSignalTool.Execute(new TimelineSignalParams { Action = "create-asset", AssetPath = SignalPath });

            var first = TimelineSignalTool.Execute(new TimelineSignalParams
            {
                Action = "add-receiver",
                ReceiverInstanceId = _receiverGo.GetInstanceID(),
                SignalAssetPath = SignalPath,
                TargetInstanceId = _targetGo.GetInstanceID(),
                TargetComponentType = nameof(TimelineSignalTestBehaviour),
                MethodName = nameof(TimelineSignalTestBehaviour.OnSignal),
            });
            Assert.IsTrue(first.Success, first.Error);

            var second = TimelineSignalTool.Execute(new TimelineSignalParams
            {
                Action = "add-receiver",
                ReceiverInstanceId = _receiverGo.GetInstanceID(),
                SignalAssetPath = SignalPath,
                TargetInstanceId = _targetGo.GetInstanceID(),
                TargetComponentType = nameof(TimelineSignalTestBehaviour),
                MethodName = nameof(TimelineSignalTestBehaviour.OnSignal),
            });

            Assert.IsFalse(second.Success);
            Assert.AreEqual("CONFLICT", second.ErrorCode);
        }

        [Test]
        public void Execute_UnknownAction_ReturnsInvalidParam()
        {
            var result = TimelineSignalTool.Execute(new TimelineSignalParams { Action = "bogus" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }

    internal class TimelineSignalTestBehaviour : MonoBehaviour
    {
        public void OnSignal() { }
    }
}
#endif
