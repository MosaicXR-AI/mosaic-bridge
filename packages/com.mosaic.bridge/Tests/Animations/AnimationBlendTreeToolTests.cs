using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Animations
{
    /// <summary>O4 §4.5 P2: nested/automatic blend trees — set-thresholds, add-child-tree, and
    /// per-child DirectBlendParameter/Mirror on set-children.</summary>
    [TestFixture]
    [Category("Animation")]
    public class AnimationBlendTreeToolTests
    {
        private const string TestDir = "Assets/MosaicBlendTreeTestTemp";
        private const string ControllerPath = TestDir + "/TestController.controller";
        private const string ClipPath = TestDir + "/TestClip.anim";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.CreateFolder("Assets", "MosaicBlendTreeTestTemp");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.layers[0].stateMachine.AddState("Locomotion");
            AnimationClipTool.Execute(new AnimationClipParams { Action = "create", Path = ClipPath });
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.DeleteAsset(TestDir);
        }

        private static void CreateTree(string blendType = "Simple1D") =>
            AnimationBlendTreeTool.Execute(new AnimationBlendTreeParams
            {
                Action = "create", ControllerPath = ControllerPath, StateName = "Locomotion",
                BlendType = blendType, BlendParameter = "Speed",
            });

        [Test]
        public void SetThresholds_AppliesAutomaticSpread()
        {
            CreateTree();

            var result = AnimationBlendTreeTool.Execute(new AnimationBlendTreeParams
            {
                Action = "set-thresholds", ControllerPath = ControllerPath, StateName = "Locomotion",
                UseAutomaticThresholds = true, MinThreshold = 0f, MaxThreshold = 5f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.UseAutomaticThresholds);
            Assert.AreEqual(5f, result.Data.MaxThreshold, 0.001f);

            var info = AnimationBlendTreeTool.Execute(new AnimationBlendTreeParams
            {
                Action = "info", ControllerPath = ControllerPath, StateName = "Locomotion",
            });
            Assert.IsTrue(info.Data.UseAutomaticThresholds, "must persist to disk");
        }

        [Test]
        public void SetThresholds_WithoutExistingBlendTree_ReturnsNotFound()
        {
            var result = AnimationBlendTreeTool.Execute(new AnimationBlendTreeParams
            {
                Action = "set-thresholds", ControllerPath = ControllerPath, StateName = "Locomotion",
                UseAutomaticThresholds = true,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void AddChildTree_Nests1DTreeUnderneath()
        {
            CreateTree("Simple1D");

            var result = AnimationBlendTreeTool.Execute(new AnimationBlendTreeParams
            {
                Action = "add-child-tree", ControllerPath = ControllerPath, StateName = "Locomotion",
                Threshold = 1f, ChildBlendType = "SimpleDirectional2D",
                ChildBlendParameter = "Forward", ChildBlendParameterY = "Turn",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.ChildCount);

            var info = AnimationBlendTreeTool.Execute(new AnimationBlendTreeParams
            {
                Action = "info", ControllerPath = ControllerPath, StateName = "Locomotion",
            });
            Assert.AreEqual(1, info.Data.Children.Length);
            Assert.IsTrue(info.Data.Children[0].IsNestedBlendTree);
            Assert.AreEqual(1f, info.Data.Children[0].Threshold, 0.001f);
        }

        [Test]
        public void AddChildTree_UnknownChildBlendType_ReturnsFail()
        {
            CreateTree();

            var result = AnimationBlendTreeTool.Execute(new AnimationBlendTreeParams
            {
                Action = "add-child-tree", ControllerPath = ControllerPath, StateName = "Locomotion",
                ChildBlendType = "Bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetChildren_AppliesDirectBlendParameterAndMirror()
        {
            CreateTree("Direct");

            var result = AnimationBlendTreeTool.Execute(new AnimationBlendTreeParams
            {
                Action = "set-children", ControllerPath = ControllerPath, StateName = "Locomotion",
                Children = new[]
                {
                    new BlendTreeChildInput
                    {
                        ClipPath = ClipPath, DirectBlendParameter = "Weight1", Mirror = true,
                    },
                },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Weight1", result.Data.Children[0].DirectBlendParameter);
            Assert.IsTrue(result.Data.Children[0].Mirror);
        }
    }
}
