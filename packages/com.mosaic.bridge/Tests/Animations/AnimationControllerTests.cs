using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Animations
{
    [TestFixture]
    [Category("Animation")]
    public class AnimationControllerTests
    {
        private const string TestDir = "Assets/MosaicTestTemp";
        private const string ControllerPath = TestDir + "/TestController.controller";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.CreateFolder("Assets", "MosaicTestTemp");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.DeleteAsset(TestDir);
        }

        [Test]
        public void Create_ReturnsOk_And_AssetExists()
        {
            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "create",
                Path   = ControllerPath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("create", result.Data.Action);
            Assert.AreEqual(ControllerPath, result.Data.Path);
            Assert.IsTrue(AssetDatabase.AssetPathExists(ControllerPath),
                "Controller asset should exist on disk");
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.Guid));
        }

        [Test]
        public void Create_MissingPath_ReturnsFail()
        {
            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "create",
                Path   = null
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Path is required"));
        }

        [Test]
        public void Info_ReturnsLayersAndParameters()
        {
            // Create first
            AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "create",
                Path   = ControllerPath
            });

            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "info",
                Path   = ControllerPath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("info", result.Data.Action);
            Assert.IsNotNull(result.Data.Layers);
            Assert.IsTrue(result.Data.Layers.Length >= 1, "Default layer should exist");
            Assert.IsNotNull(result.Data.Parameters);
        }

        [Test]
        public void AddParameter_And_Verify()
        {
            // Create controller
            AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "create",
                Path   = ControllerPath
            });

            // Add a float parameter
            var addResult = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action        = "add-parameter",
                Path          = ControllerPath,
                ParameterName = "Speed",
                ParameterType = "Float"
            });

            Assert.IsTrue(addResult.Success, addResult.Error);
            Assert.AreEqual("add-parameter", addResult.Data.Action);
            Assert.AreEqual("Speed", addResult.Data.AddedParameterName);

            // Verify via info
            var infoResult = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "info",
                Path   = ControllerPath
            });

            Assert.IsTrue(infoResult.Success, infoResult.Error);
            Assert.IsTrue(infoResult.Data.Parameters.Length >= 1,
                "Should have at least 1 parameter after add");
            Assert.AreEqual("Speed", infoResult.Data.Parameters[0].Name);
            Assert.AreEqual("Float", infoResult.Data.Parameters[0].Type);
        }

        [Test]
        public void RemoveParameter_And_Verify()
        {
            // Create controller and add parameter
            AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "create",
                Path   = ControllerPath
            });

            AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action        = "add-parameter",
                Path          = ControllerPath,
                ParameterName = "Health",
                ParameterType = "Int"
            });

            // Remove it
            var removeResult = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action         = "remove-parameter",
                Path           = ControllerPath,
                ParameterIndex = 0
            });

            Assert.IsTrue(removeResult.Success, removeResult.Error);
            Assert.AreEqual("remove-parameter", removeResult.Data.Action);
            Assert.AreEqual(0, removeResult.Data.RemovedParameterIndex);

            // Verify empty
            var infoResult = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "info",
                Path   = ControllerPath
            });

            Assert.IsTrue(infoResult.Success);
            Assert.AreEqual(0, infoResult.Data.Parameters.Length);
        }

        [Test]
        public void AddLayer_And_Verify()
        {
            AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "create",
                Path   = ControllerPath
            });

            var addResult = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action    = "add-layer",
                Path      = ControllerPath,
                LayerName = "UpperBody"
            });

            Assert.IsTrue(addResult.Success, addResult.Error);
            Assert.AreEqual("add-layer", addResult.Data.Action);
            Assert.AreEqual("UpperBody", addResult.Data.AddedLayerName);

            // Verify via info
            var infoResult = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "info",
                Path   = ControllerPath
            });

            Assert.IsTrue(infoResult.Success);
            Assert.IsTrue(infoResult.Data.Layers.Length >= 2,
                "Should have at least 2 layers (Base + UpperBody)");
        }

        [Test]
        public void InvalidAction_ReturnsFail_WithValidActions()
        {
            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "invalid-action"
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("create"), "Error should list valid actions");
            Assert.IsTrue(result.Error.Contains("info"), "Error should list valid actions");
        }

        [Test]
        public void Info_NotFound_ReturnsFail()
        {
            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "info",
                Path   = "Assets/DoesNotExist.controller"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void AddParameter_InvalidType_ReturnsFail()
        {
            AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "create",
                Path   = ControllerPath
            });

            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action        = "add-parameter",
                Path          = ControllerPath,
                ParameterName = "Foo",
                ParameterType = "Vector3"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // O4 §4.5 P1: set-layer/remove-layer. AnimatorController.layers hands back a fresh copy
        // on every read -- mutating an element without writing the whole array back is a silent
        // no-op, which is exactly the bug this exercises: WeightAndBlendingModePersist below fails
        // if that write-back is ever dropped.
        [Test]
        public void SetLayer_WeightAndBlendingModePersist()
        {
            AnimationControllerTool.Execute(new AnimationControllerParams { Action = "create", Path = ControllerPath });
            AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "add-layer", Path = ControllerPath, LayerName = "Upper"
            });

            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "set-layer", Path = ControllerPath, LayerIndex = 1,
                LayerWeight = 0.5f, BlendingMode = "Additive", IKPass = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.5f, result.Data.LayerWeight, 0.001f);
            Assert.AreEqual("Additive", result.Data.BlendingMode);
            Assert.IsTrue(result.Data.IKPass);

            // Re-load fresh from disk -- proves the write-back actually persisted, not just that
            // the in-memory `layer` object we already had a reference to looked right.
            var reloaded = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
            Assert.AreEqual(0.5f, reloaded.layers[1].defaultWeight, 0.001f);
            Assert.AreEqual(UnityEditor.Animations.AnimatorLayerBlendingMode.Additive, reloaded.layers[1].blendingMode);
            Assert.IsTrue(reloaded.layers[1].iKPass);
        }

        [Test]
        public void SetLayer_UnknownBlendingMode_ReturnsFail()
        {
            AnimationControllerTool.Execute(new AnimationControllerParams { Action = "create", Path = ControllerPath });
            AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "add-layer", Path = ControllerPath, LayerName = "Upper"
            });

            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "set-layer", Path = ControllerPath, LayerIndex = 1, BlendingMode = "Nonsense",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetLayer_AvatarMaskPath_AssignsTheMask()
        {
            const string maskPath = TestDir + "/TestMask.mask";
            AnimationControllerTool.Execute(new AnimationControllerParams { Action = "create", Path = ControllerPath });
            AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "add-layer", Path = ControllerPath, LayerName = "Upper"
            });
            AnimationAvatarMaskTool.Execute(new AnimationAvatarMaskParams { Action = "create", MaskPath = maskPath });

            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "set-layer", Path = ControllerPath, LayerIndex = 1, AvatarMaskPath = maskPath,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(maskPath, result.Data.AvatarMaskPath);
        }

        [Test]
        public void RemoveLayer_RemovesIt()
        {
            AnimationControllerTool.Execute(new AnimationControllerParams { Action = "create", Path = ControllerPath });
            AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "add-layer", Path = ControllerPath, LayerName = "Upper"
            });

            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "remove-layer", Path = ControllerPath, LayerIndex = 1,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Upper", result.Data.LayerName);
            var reloaded = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
            Assert.AreEqual(1, reloaded.layers.Length);
        }

        [Test]
        public void RemoveLayer_OutOfRange_ReturnsFail()
        {
            AnimationControllerTool.Execute(new AnimationControllerParams { Action = "create", Path = ControllerPath });

            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "remove-layer", Path = ControllerPath, LayerIndex = 5,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
        }

        // O4 §4.5 P2: "one controller, many characters" — an override controller retargets clips
        // without duplicating the whole state machine.
        [Test]
        public void CreateOverride_ReplacesAMatchedClipByName()
        {
            AnimationControllerTool.Execute(new AnimationControllerParams { Action = "create", Path = ControllerPath });
            var baseController = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
            var state = baseController.layers[0].stateMachine.AddState("Idle");
            var originalClip = new UnityEngine.AnimationClip { name = "Base_Idle" };
            AssetDatabase.AddObjectToAsset(originalClip, baseController);
            state.motion = originalClip;
            AssetDatabase.SaveAssets();

            const string newClipPath = TestDir + "/Character_Idle.anim";
            AnimationClipTool.Execute(new AnimationClipParams { Action = "create", Path = newClipPath, ClipName = "Character_Idle" });

            const string overridePath = TestDir + "/TestOverride.overrideController";
            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "create-override", Path = overridePath, BaseControllerPath = ControllerPath,
                Overrides = new[]
                {
                    new OverrideClipInput { OriginalClipName = "Base_Idle", NewClipPath = newClipPath },
                },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.OverrideCount);

            var overrideController = AssetDatabase.LoadAssetAtPath<UnityEngine.AnimatorOverrideController>(overridePath);
            Assert.IsNotNull(overrideController);
            Assert.AreEqual("Character_Idle", overrideController[originalClip].name);
        }

        [Test]
        public void CreateOverride_UnmatchedOriginalClip_ReturnsFail()
        {
            AnimationControllerTool.Execute(new AnimationControllerParams { Action = "create", Path = ControllerPath });
            const string newClipPath = TestDir + "/Character_Idle2.anim";
            AnimationClipTool.Execute(new AnimationClipParams { Action = "create", Path = newClipPath });

            var result = AnimationControllerTool.Execute(new AnimationControllerParams
            {
                Action = "create-override", Path = TestDir + "/TestOverride2.overrideController",
                BaseControllerPath = ControllerPath,
                Overrides = new[]
                {
                    new OverrideClipInput { OriginalClipName = "NoSuchClip", NewClipPath = newClipPath },
                },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void AddBehaviour_AttachesTheCompiledBehaviourType()
        {
            AnimationControllerTool.Execute(new AnimationControllerParams { Action = "create", Path = ControllerPath });
            AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "Attack",
            });

            var result = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add-behaviour", ControllerPath = ControllerPath, StateName = "Attack",
                BehaviourTypeName = nameof(TestStateMachineBehaviour),
            });

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains(nameof(TestStateMachineBehaviour), result.Data.AddedBehaviourTypeName);

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ControllerPath);
            var attackState = controller.layers[0].stateMachine.states[0].state;
            Assert.AreEqual(1, attackState.behaviours.Length);
            Assert.IsInstanceOf<TestStateMachineBehaviour>(attackState.behaviours[0]);
        }

        [Test]
        public void AddBehaviour_UnknownType_ReturnsFail()
        {
            AnimationControllerTool.Execute(new AnimationControllerParams { Action = "create", Path = ControllerPath });
            AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add", ControllerPath = ControllerPath, StateName = "Attack",
            });

            var result = AnimationStateTool.Execute(new AnimationStateParams
            {
                Action = "add-behaviour", ControllerPath = ControllerPath, StateName = "Attack",
                BehaviourTypeName = "NoSuchBehaviour_zzz",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}
