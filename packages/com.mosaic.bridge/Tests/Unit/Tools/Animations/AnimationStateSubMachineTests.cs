using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Unit.Tools.Animations
{
    /// <summary>
    /// O4 §4.5 P1: animation/state gained ParentStateMachinePath + Position (every generated
    /// state used to land at (0,0) at the layer root, making Animator captures of anything but a
    /// flat state machine unreadable), plus add-sub-machine, set-default, and set-settings.
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("Animation")]
    public class AnimationStateSubMachineTests
    {
        private const string ControllerPath = "Assets/MosaicAnimSubMachineTestController.controller";

        [SetUp]
        public void SetUp()
        {
            AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);
        }

        [Test]
        public void Add_WithPosition_PlacesStateThere()
        {
            var result = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "Run",
                Position = new[] { 300f, 150f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(300f, result.Data.PositionX, 0.01f);
            Assert.AreEqual(150f, result.Data.PositionY, 0.01f);
        }

        [Test]
        public void AddSubMachine_ThenAddStateInsideIt_NestsCorrectly()
        {
            var subResult = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add-sub-machine", ControllerPath = ControllerPath, SubMachineName = "Combat",
            });
            Assert.IsTrue(subResult.Success, subResult.Error);
            Assert.AreEqual("Combat", subResult.Data.SubMachineName);

            var stateResult = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "Slash",
                ParentStateMachinePath = "Combat",
            });
            Assert.IsTrue(stateResult.Success, stateResult.Error);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var root = controller.layers[0].stateMachine;
            var combat = System.Array.Find(root.stateMachines, sm => sm.stateMachine.name == "Combat").stateMachine;
            Assert.IsNotNull(combat);
            Assert.IsTrue(System.Array.Exists(combat.states, cs => cs.state.name == "Slash"),
                "Slash must be nested inside Combat, not at the layer root");
            Assert.IsFalse(System.Array.Exists(root.states, cs => cs.state.name == "Slash"),
                "Slash must NOT also appear at the layer root");
        }

        [Test]
        public void Add_UnknownParentPath_ReturnsNotFound()
        {
            var result = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "Slash",
                ParentStateMachinePath = "NoSuchMachine",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void SetDefault_ChangesTheOwningMachinesDefaultState()
        {
            AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "Run",
            });

            var result = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "set-default", ControllerPath = ControllerPath, StateName = "Run",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.IsDefault);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.AreEqual("Run", controller.layers[0].stateMachine.defaultState.name);
        }

        [Test]
        public void SetSettings_AppliesSpeedMirrorAndTag()
        {
            AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "Run",
            });

            var result = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "set-settings", ControllerPath = ControllerPath, StateName = "Run",
                Speed = 1.5f, Mirror = true, Tag = "Locomotion",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1.5f, result.Data.Speed, 0.001f);
            Assert.IsTrue(result.Data.Mirror);
            Assert.AreEqual("Locomotion", result.Data.Tag);
        }

        [Test]
        public void Info_ReportsIsDefaultForStateInsideASubMachine()
        {
            AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add-sub-machine", ControllerPath = ControllerPath, SubMachineName = "Combat",
            });
            AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "Slash",
                ParentStateMachinePath = "Combat",
            });
            AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "set-default", ControllerPath = ControllerPath, StateName = "Slash",
                ParentStateMachinePath = "Combat",
            });

            var result = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "info", ControllerPath = ControllerPath, StateName = "Slash",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.IsDefault,
                "IsDefault must compare against Slash's own parent machine (Combat), not the layer root");
        }
    }
}
