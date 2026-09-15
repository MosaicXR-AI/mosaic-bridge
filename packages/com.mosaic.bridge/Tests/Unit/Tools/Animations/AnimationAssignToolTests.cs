using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Unit.Tools.Animations
{
    [TestFixture]
    [Category("Unit")]
    [Category("Animation")]
    public class AnimationAssignToolTests
    {
        private const string ControllerPath = "Assets/MosaicAnimAssignTestController.controller";
        private const string ClipPath = "Assets/MosaicAnimAssignTestClip.anim";
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("MosaicAnimAssignProbe");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (AssetDatabase.LoadAssetAtPath<Object>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);
            if (AssetDatabase.LoadAssetAtPath<Object>(ClipPath) != null)
                AssetDatabase.DeleteAsset(ClipPath);
        }

        [Test]
        public void Assign_Animator_CreatesComponentAndSetsController()
        {
            UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            var result = AnimationAssignTool.Execute(new AnimationAssignParams
            {
                GameObjectName = "MosaicAnimAssignProbe",
                ControllerPath = ControllerPath,
                ApplyRootMotion = true,
                UpdateMode = "Fixed",
                CullingMode = "AlwaysAnimate",
            });

            Assert.IsTrue(result.Success, result.Error);
            var animator = _go.GetComponent<Animator>();
            Assert.IsNotNull(animator);
            Assert.IsNotNull(animator.runtimeAnimatorController);
            Assert.IsTrue(animator.applyRootMotion);
            Assert.AreEqual(AnimatorUpdateMode.Fixed, animator.updateMode);
            Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode);
        }

        [Test]
        public void Assign_Animator_ReusesExistingComponent()
        {
            var existing = _go.AddComponent<Animator>();
            UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            var result = AnimationAssignTool.Execute(new AnimationAssignParams
            {
                GameObjectName = "MosaicAnimAssignProbe", ControllerPath = ControllerPath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, _go.GetComponents<Animator>().Length, "must not add a second Animator");
            Assert.AreSame(existing, _go.GetComponent<Animator>());
        }

        [Test]
        public void Assign_UnknownUpdateMode_ReturnsFail()
        {
            var result = AnimationAssignTool.Execute(new AnimationAssignParams
            {
                GameObjectName = "MosaicAnimAssignProbe", UpdateMode = "Nonsense"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Assign_MissingController_ReturnsNotFound()
        {
            var result = AnimationAssignTool.Execute(new AnimationAssignParams
            {
                GameObjectName = "MosaicAnimAssignProbe", ControllerPath = "Assets/NoSuchController.controller"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Assign_Legacy_AddsAnimationComponentWithClip()
        {
            var clip = new AnimationClip { name = "Clip" };
            AssetDatabase.CreateAsset(clip, ClipPath);
            AssetDatabase.SaveAssets();

            var result = AnimationAssignTool.Execute(new AnimationAssignParams
            {
                GameObjectName = "MosaicAnimAssignProbe", Legacy = true, ClipPath = ClipPath
            });

            Assert.IsTrue(result.Success, result.Error);
            var animation = _go.GetComponent<Animation>();
            Assert.IsNotNull(animation);
            Assert.IsNotNull(animation.clip);
            Assert.AreEqual("MosaicAnimAssignTestClip", animation.clip.name);
        }

        [Test]
        public void Assign_Legacy_WithoutClipPath_ReturnsInvalidParam()
        {
            var result = AnimationAssignTool.Execute(new AnimationAssignParams
            {
                GameObjectName = "MosaicAnimAssignProbe", Legacy = true
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Assign_MissingGameObject_ReturnsNotFound()
        {
            var result = AnimationAssignTool.Execute(new AnimationAssignParams
            {
                GameObjectName = "MosaicNoSuchObject_zzz"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}
