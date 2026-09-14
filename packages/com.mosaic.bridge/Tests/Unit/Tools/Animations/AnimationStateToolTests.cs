using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Unit.Tools.Animations
{
    /// <summary>
    /// L19: animation/state's set-motion used AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;,
    /// which silently returns the FIRST matching sub-asset — for a multi-clip container (a
    /// multi-take FBX in practice) that is always the same clip no matter which take is wanted.
    /// A container with multiple AnimationClip sub-assets at one path (built here via
    /// AddObjectToAsset, which mirrors how an imported FBX embeds several clips in one file)
    /// stands in for a real multi-take FBX without needing an actual model import pipeline.
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("Animation")]
    public class AnimationStateToolTests
    {
        private const string ControllerPath = "Assets/MosaicAnimStateTestController.controller";
        private const string MultiClipPath = "Assets/MosaicAnimStateTestMultiClip.asset";

        [SetUp]
        public void SetUp()
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddLayer("Base"); // ensure layer 0 exists beyond the default
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);
            if (AssetDatabase.LoadAssetAtPath<Object>(MultiClipPath) != null)
                AssetDatabase.DeleteAsset(MultiClipPath);
        }

        private static void CreateMultiClipContainer()
        {
            var clipA = new AnimationClip { name = "TakeA" };
            var clipB = new AnimationClip { name = "TakeB" };
            AssetDatabase.CreateAsset(clipA, MultiClipPath);
            AssetDatabase.AddObjectToAsset(clipB, MultiClipPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MultiClipPath, ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void SetMotion_MultiClipContainer_WithoutClipName_StillResolvesAClip()
        {
            // Unchanged/backward-compatible behavior: omitting ClipName still resolves to
            // whichever clip LoadAssetAtPath's own sub-asset ordering picks (the pre-existing
            // behavior for a single-clip asset, and the ambiguous case ClipName exists to fix).
            CreateMultiClipContainer();
            AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "MyState"
            });

            var result = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "set-motion", ControllerPath = ControllerPath, StateName = "MyState",
                ClipPath = MultiClipPath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.MotionName);
        }

        [Test]
        public void SetMotion_MultiClipContainer_WithClipName_PicksThatExactClip()
        {
            CreateMultiClipContainer();
            AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "MyState"
            });

            var result = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "set-motion", ControllerPath = ControllerPath, StateName = "MyState",
                ClipPath = MultiClipPath, ClipName = "TakeB"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("TakeB", result.Data.MotionName);

            var state = AnimationToolHelpers.FindState(
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath), "MyState", 0);
            Assert.AreEqual("TakeB", state.motion.name);
        }

        [Test]
        public void SetMotion_UnknownClipName_ListsAvailableClips()
        {
            CreateMultiClipContainer();
            AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "MyState"
            });

            var result = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "set-motion", ControllerPath = ControllerPath, StateName = "MyState",
                ClipPath = MultiClipPath, ClipName = "NoSuchTake"
            });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("TakeB", result.Error);
            StringAssert.Contains("Available:", result.Error);
        }
    }
}
