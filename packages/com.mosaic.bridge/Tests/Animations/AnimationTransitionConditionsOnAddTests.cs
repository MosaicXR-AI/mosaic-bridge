using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Animations
{
    /// <summary>
    /// H-1 follow-up: `animation/transition`'s "add" action never read the `conditions`
    /// parameter at all — only "set-conditions" (a separate call, against an existing
    /// transition's index) applied them. A caller who supplied `conditions` on "add" got
    /// `success: true` and `ConditionCount: 0` with no error, and an invalid `mode` inside
    /// that array was never validated either, since nothing ever looked at it.
    /// </summary>
    [TestFixture]
    [Category("Animation")]
    public class AnimationTransitionConditionsOnAddTests
    {
        private const string TestDir = "Assets/MosaicTestTemp";
        private const string ControllerPath = TestDir + "/TestConditionsController.controller";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.CreateFolder("Assets", "MosaicTestTemp");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Overload", UnityEngine.AnimatorControllerParameterType.Float);
            var sm = controller.layers[0].stateMachine;
            sm.AddState("Idle");
            sm.AddState("Overloaded");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.DeleteAsset(TestDir);
        }

        private static AnimationTransitionParams AddParams(TransitionConditionInput[] conditions) => new()
        {
            Action = "add",
            ControllerPath = ControllerPath,
            SourceStateName = "Idle",
            DestinationStateName = "Overloaded",
            Conditions = conditions,
        };

        [Test]
        public void Add_WithConditions_AppliesThem_NotJustReportsZero()
        {
            var result = AnimationTransitionTool.Execute(AddParams(new[]
            {
                new TransitionConditionInput { Mode = "If", ParameterName = "Overload" },
            }));

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.ConditionCount,
                "a condition supplied on 'add' must be applied, not silently dropped");
        }

        [Test]
        public void Add_WithInvalidMode_FailsInsteadOfSilentlySucceeding()
        {
            var result = AnimationTransitionTool.Execute(AddParams(new[]
            {
                new TransitionConditionInput { Mode = "BOGUS_MODE", ParameterName = "Overload" },
            }));

            Assert.IsFalse(result.Success,
                "success:true with an unrecognised mode is the exact failure mode this fixes " +
                "-- a result that looks correct and is not");
            StringAssert.Contains("BOGUS_MODE", result.Error);
        }

        [Test]
        public void Add_WithNoConditions_StillWorks_ZeroIsHonest()
        {
            // Conditions are optional on 'add' -- a transition with none is a real, common case
            // and must not be treated as an error.
            var result = AnimationTransitionTool.Execute(AddParams(null));

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.ConditionCount);
        }

        [Test]
        public void SetConditions_StillWorks_UnaffectedByThisFix()
        {
            var added = AnimationTransitionTool.Execute(AddParams(null));
            Assert.IsTrue(added.Success, added.Error);

            var result = AnimationTransitionTool.Execute(new AnimationTransitionParams
            {
                Action = "set-conditions",
                ControllerPath = ControllerPath,
                SourceStateName = "Idle",
                TransitionIndex = 0,
                Conditions = new[]
                {
                    new TransitionConditionInput { Mode = "Greater", ParameterName = "Overload", Threshold = 0.5f },
                },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.ConditionCount);
        }
    }
}
