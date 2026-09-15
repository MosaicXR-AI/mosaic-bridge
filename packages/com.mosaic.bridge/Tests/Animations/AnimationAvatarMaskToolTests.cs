using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Animations
{
    /// <summary>O4 §4.5 P1: animation/avatar-mask, used by animation/controller's set-layer
    /// AvatarMaskPath for override layers ("upper-body aim while running").</summary>
    [TestFixture]
    [Category("Animation")]
    public class AnimationAvatarMaskToolTests
    {
        private const string TestDir = "Assets/MosaicAvatarMaskTestTemp";
        private const string MaskPath = TestDir + "/TestMask.mask";
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.CreateFolder("Assets", "MosaicAvatarMaskTestTemp");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestDir))
                AssetDatabase.DeleteAsset(TestDir);
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private static AnimationAvatarMaskResult Execute(AnimationAvatarMaskParams p)
        {
            var result = AnimationAvatarMaskTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            return result.Data;
        }

        [Test]
        public void Create_MakesAnAssetWithEveryBodyPartActive()
        {
            Execute(new AnimationAvatarMaskParams { Action = "create", MaskPath = MaskPath });

            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            Assert.IsNotNull(mask);
            Assert.IsTrue(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm));
            Assert.IsTrue(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg));
        }

        [Test]
        public void Create_Twice_SecondCallFails()
        {
            Execute(new AnimationAvatarMaskParams { Action = "create", MaskPath = MaskPath });

            var result = AnimationAvatarMaskTool.Execute(new AnimationAvatarMaskParams
            {
                Action = "create", MaskPath = MaskPath
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("CONFLICT", result.ErrorCode);
        }

        [Test]
        public void SetBodyPart_TurnsOffLegsForAnUpperBodyMask()
        {
            Execute(new AnimationAvatarMaskParams { Action = "create", MaskPath = MaskPath });

            Execute(new AnimationAvatarMaskParams
            {
                Action = "set-body-part", MaskPath = MaskPath, BodyPart = "LeftLeg", Active = false
            });
            Execute(new AnimationAvatarMaskParams
            {
                Action = "set-body-part", MaskPath = MaskPath, BodyPart = "RightLeg", Active = false
            });

            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            Assert.IsFalse(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg));
            Assert.IsFalse(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg));
            Assert.IsTrue(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm),
                "only the legs were turned off");
        }

        [Test]
        public void SetBodyPart_UnknownName_ReturnsFail()
        {
            Execute(new AnimationAvatarMaskParams { Action = "create", MaskPath = MaskPath });

            var result = AnimationAvatarMaskTool.Execute(new AnimationAvatarMaskParams
            {
                Action = "set-body-part", MaskPath = MaskPath, BodyPart = "Tail", Active = true
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void AddTransformPath_ResolvesAgainstSceneHierarchy()
        {
            Execute(new AnimationAvatarMaskParams { Action = "create", MaskPath = MaskPath });
            _root = new GameObject("MosaicAvatarMaskTestRig");
            var child = new GameObject("Spine");
            child.transform.SetParent(_root.transform);

            var result = Execute(new AnimationAvatarMaskParams
            {
                Action = "add-transform-path", MaskPath = MaskPath,
                RootGameObjectName = "MosaicAvatarMaskTestRig", TransformPath = "Spine",
            });

            Assert.AreEqual(1, result.TransformCount);
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            Assert.AreEqual(1, mask.transformCount);
            StringAssert.Contains("Spine", mask.GetTransformPath(0));
        }

        [Test]
        public void AddTransformPath_UnknownChild_ReturnsFail()
        {
            Execute(new AnimationAvatarMaskParams { Action = "create", MaskPath = MaskPath });
            _root = new GameObject("MosaicAvatarMaskTestRig2");

            var result = AnimationAvatarMaskTool.Execute(new AnimationAvatarMaskParams
            {
                Action = "add-transform-path", MaskPath = MaskPath,
                RootGameObjectName = "MosaicAvatarMaskTestRig2", TransformPath = "NoSuchChild",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Info_ReportsBodyPartsAndTransformPaths()
        {
            Execute(new AnimationAvatarMaskParams { Action = "create", MaskPath = MaskPath });
            Execute(new AnimationAvatarMaskParams
            {
                Action = "set-body-part", MaskPath = MaskPath, BodyPart = "Head", Active = false
            });
            _root = new GameObject("MosaicAvatarMaskTestRig3");
            Execute(new AnimationAvatarMaskParams
            {
                Action = "add-transform-path", MaskPath = MaskPath,
                RootGameObjectName = "MosaicAvatarMaskTestRig3",
            });

            var info = Execute(new AnimationAvatarMaskParams { Action = "info", MaskPath = MaskPath });

            Assert.AreEqual(1, info.TransformCount);
            Assert.IsTrue(System.Array.Exists(info.BodyParts, b => b.Part == "Head" && !b.Active));
            Assert.IsTrue(System.Array.Exists(info.BodyParts, b => b.Part == "LeftArm" && b.Active));
        }
    }
}
