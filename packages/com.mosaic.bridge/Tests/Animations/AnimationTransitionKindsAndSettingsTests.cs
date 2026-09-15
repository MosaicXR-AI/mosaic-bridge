using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Animations
{
    /// <summary>
    /// O4 §4.5 P1: animation/transition gained SourceKind (state/anyState/entry),
    /// DestinationKind (state/exit), set-settings, and info. AnyState/entry transitions
    /// previously had no route at all -- "hit/death any state -> stagger" is exactly the
    /// pattern that needs AnyState, and an entry transition (Unity's AnimatorTransition, not
    /// AnimatorStateTransition) needs its own settings restrictions honoured, not guessed.
    /// </summary>
    [TestFixture]
    [Category("Animation")]
    public class AnimationTransitionKindsAndSettingsTests
    {
        private const string TestDir = "Assets/MosaicTestTemp2";
        private const string ControllerPath = TestDir + "/TestKindsController.controller";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.CreateFolder("Assets", "MosaicTestTemp2");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = controller.layers[0].stateMachine;
            sm.AddState("Idle");
            sm.AddState("Stagger");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.DeleteAsset(TestDir);
        }

        [Test]
        public void Add_AnyState_CreatesTransitionOnTheStateMachine()
        {
            var result = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceKind = "anyState", DestinationStateName = "Stagger",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("anyState", result.Data.SourceKind);
            Assert.AreEqual("Stagger", result.Data.DestinationStateName);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.AreEqual(1, controller.layers[0].stateMachine.anyStateTransitions.Length);
        }

        [Test]
        public void Add_Entry_CreatesTransitionAndIgnoresExitTimeSettings()
        {
            var result = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceKind = "entry", DestinationStateName = "Idle",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("entry", result.Data.SourceKind);
            Assert.IsNull(result.Data.ExitTime, "an entry AnimatorTransition has no ExitTime");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.AreEqual(1, controller.layers[0].stateMachine.entryTransitions.Length);
        }

        [Test]
        public void Add_ExitDestination_OnState_CreatesExitTransition()
        {
            var result = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceStateName = "Idle", DestinationKind = "exit",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.IsExit);
        }

        [Test]
        public void Add_ExitDestination_OnAnyState_Rejected()
        {
            var result = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceKind = "anyState", DestinationKind = "exit",
            });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("only valid when SourceKind is 'state'", result.Error);
        }

        [Test]
        public void SetSettings_OnStateTransition_AppliesExitTimeAndInterruptionSource()
        {
            var add = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceStateName = "Idle", DestinationStateName = "Stagger",
            });
            Assert.IsTrue(add.Success, add.Error);

            var result = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "set-settings", ControllerPath = ControllerPath,
                SourceStateName = "Idle", TransitionIndex = 0,
                ExitTime = 0.9f, InterruptionSource = "Source",
                CanTransitionToSelf = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.9f, result.Data.ExitTime.Value, 0.001f);
            Assert.AreEqual("Source", result.Data.InterruptionSource);
            Assert.IsTrue(result.Data.CanTransitionToSelf.Value);
        }

        [Test]
        public void SetSettings_UnknownInterruptionSource_ReturnsFail()
        {
            AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceStateName = "Idle", DestinationStateName = "Stagger",
            });

            var result = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "set-settings", ControllerPath = ControllerPath,
                SourceStateName = "Idle", TransitionIndex = 0,
                InterruptionSource = "Nonsense",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetSettings_OnEntryTransition_RejectsStateOnlyFields()
        {
            AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceKind = "entry", DestinationStateName = "Idle",
            });

            var result = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "set-settings", ControllerPath = ControllerPath,
                SourceKind = "entry", TransitionIndex = 0,
                ExitTime = 0.5f,
            });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("no ExitTime", result.Error);
        }

        [Test]
        public void Remove_AnyStateTransition_RemovesFromTheRightArray()
        {
            AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceKind = "anyState", DestinationStateName = "Stagger",
            });

            var result = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "remove", ControllerPath = ControllerPath,
                SourceKind = "anyState", TransitionIndex = 0,
            });

            Assert.IsTrue(result.Success, result.Error);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.AreEqual(0, controller.layers[0].stateMachine.anyStateTransitions.Length);
        }

        [Test]
        public void Info_ReportsAllKindsAcrossTheLayer()
        {
            AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceStateName = "Idle", DestinationStateName = "Stagger",
            });
            AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceKind = "anyState", DestinationStateName = "Stagger",
            });
            AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "add", ControllerPath = ControllerPath,
                SourceKind = "entry", DestinationStateName = "Idle",
            });

            var result = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "info", ControllerPath = ControllerPath, SourceStateName = "Idle",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.Transitions.Length);
            CollectionAssert.AreEquivalent(new[] { "state", "anyState", "entry" },
                System.Array.ConvertAll(result.Data.Transitions, t => t.SourceKind));
        }
    }
}
