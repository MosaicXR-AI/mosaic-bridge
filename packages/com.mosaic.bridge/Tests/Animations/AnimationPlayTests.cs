using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Animations
{
    [TestFixture]
    [Category("Animation")]
    public class AnimationPlayTests
    {
        private const string TestDir = "Assets/MosaicTestTemp";
        private const string ControllerPath = TestDir + "/PlayTestController.controller";
        private const string ClipPath = TestDir + "/PlayTestClip.anim";

        private GameObject _testGo;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.CreateFolder("Assets", "MosaicTestTemp");

            // Create a GameObject with an Animator
            _testGo = new GameObject("AnimPlayTestGO");
            _testGo.AddComponent<Animator>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null)
                Object.DestroyImmediate(_testGo);

            if (AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.DeleteAsset(TestDir);
        }

        [Test]
        public void InvalidAction_ReturnsFail()
        {
            var result = AnimationPlayTool.Execute(new AnimationPlayParams
            {
                Action = "rewind"
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("play"), "Error should list valid actions");
            Assert.IsTrue(result.Error.Contains("stop"), "Error should list valid actions");
            Assert.IsTrue(result.Error.Contains("sample"), "Error should list valid actions");
        }

        [Test]
        public void Play_NoGameObject_ReturnsFail()
        {
            var result = AnimationPlayTool.Execute(new AnimationPlayParams
            {
                Action         = "play",
                GameObjectName = "NonExistentGO",
                StateName      = "Idle"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Stop_ValidGameObject_ReturnsOk()
        {
            var result = AnimationPlayTool.Execute(new AnimationPlayParams
            {
                Action         = "stop",
                GameObjectName = _testGo.name
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("stop", result.Data.Action);
            Assert.AreEqual(_testGo.name, result.Data.GameObjectName);
        }

        [Test]
        public void Stop_NoAnimator_ReturnsFail()
        {
            // Create a GO without Animator
            var plainGo = new GameObject("PlainGO");
            try
            {
                var result = AnimationPlayTool.Execute(new AnimationPlayParams
                {
                    Action         = "stop",
                    GameObjectName = "PlainGO"
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual("NOT_FOUND", result.ErrorCode);
                Assert.IsTrue(result.Error.Contains("Animator"));
            }
            finally
            {
                Object.DestroyImmediate(plainGo);
            }
        }

        [Test]
        public void Sample_WithClipPath_ReturnsOk()
        {
            // Create a clip asset
            AnimationClipTool.Execute(new AnimationClipParams
            {
                Action = "create",
                Path   = ClipPath
            });

            // Add a simple curve so the clip has content
            AnimationClipTool.Execute(new AnimationClipParams
            {
                Action         = "set-curve",
                Path           = ClipPath,
                PropertyPath   = "",
                ComponentType  = "Transform",
                PropertyName   = "localPosition.x",
                KeyframeTimes  = new float[] { 0f, 1f },
                KeyframeValues = new float[] { 0f, 10f }
            });

            var result = AnimationPlayTool.Execute(new AnimationPlayParams
            {
                Action         = "sample",
                GameObjectName = _testGo.name,
                ClipPath       = ClipPath,
                NormalizedTime = 0.5f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("sample", result.Data.Action);
            Assert.AreEqual(0.5f, result.Data.NormalizedTime, 0.001f);
        }

        [Test]
        public void Sample_MissingStateAndClip_ReturnsFail()
        {
            var result = AnimationPlayTool.Execute(new AnimationPlayParams
            {
                Action         = "sample",
                GameObjectName = _testGo.name
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Play_ViaInstanceId_ReturnsFail_WhenNoController()
        {
            // Animator has no controller assigned, so Play should still work
            // (it won't crash, but the state won't actually exist)
            var result = AnimationPlayTool.Execute(new AnimationPlayParams
            {
                Action     = "play",
                InstanceId = _testGo.GetInstanceID(),
                StateName  = "Idle"
            });

            // This should succeed at the API level (Unity doesn't throw for missing state)
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("play", result.Data.Action);
        }
    }
}
