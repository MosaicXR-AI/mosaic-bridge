using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Animations;

namespace Mosaic.Bridge.Tests.Animations
{
    [TestFixture]
    [Category("Animation")]
    public class AnimationIkSetupTests
    {
        private GameObject _testGo;
        private const string TestSavePath = "Assets/MosaicTestTemp/IK/";

        [SetUp]
        public void SetUp()
        {
            _testGo = new GameObject("IKTestGO");
            new GameObject("Bone1").transform.SetParent(_testGo.transform);
            new GameObject("Bone2").transform.SetParent(_testGo.transform);
            new GameObject("IKTarget");
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null) Object.DestroyImmediate(_testGo);
            var target = GameObject.Find("IKTarget");
            if (target != null) Object.DestroyImmediate(target);
            if (AssetDatabase.IsValidFolder("Assets/MosaicTestTemp"))
                AssetDatabase.DeleteAsset("Assets/MosaicTestTemp");
        }

        [Test]
        public void Fabrik_Solver_CreatesValidScript()
        {
            var result = AnimationIkSetupTool.Execute(new AnimationIkSetupParams
            {
                GameObjectName = "IKTestGO",
                Solver = "fabrik",
                ChainBones = new[] { "Bone1", "Bone2" },
                Target = "IKTarget",
                SavePath = TestSavePath
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("fabrik", result.Data.Solver);
            Assert.AreEqual(2, result.Data.ChainLength);
            Assert.IsNotNull(result.Data.ScriptPath);
            Assert.IsTrue(File.Exists(Application.dataPath.Replace("/Assets", "") + "/" + result.Data.ScriptPath));
        }

        [Test]
        public void Ccd_Solver_CreatesValidScript()
        {
            var result = AnimationIkSetupTool.Execute(new AnimationIkSetupParams
            {
                GameObjectName = "IKTestGO",
                Solver = "ccd",
                ChainBones = new[] { "Bone1", "Bone2" },
                Target = "IKTarget",
                SavePath = TestSavePath
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("ccd", result.Data.Solver);
        }

        [Test]
        public void Limb_Solver_CreatesValidScript()
        {
            var result = AnimationIkSetupTool.Execute(new AnimationIkSetupParams
            {
                GameObjectName = "IKTestGO",
                Solver = "limb",
                ChainBones = new[] { "Bone1", "Bone2" },
                Target = "IKTarget",
                SavePath = TestSavePath
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("limb", result.Data.Solver);
        }

        [Test]
        public void Invalid_Solver_Returns_Error()
        {
            var result = AnimationIkSetupTool.Execute(new AnimationIkSetupParams
            {
                GameObjectName = "IKTestGO",
                Solver = "invalid_solver",
                ChainBones = new[] { "Bone1", "Bone2" },
                Target = "IKTarget"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void Empty_Chain_Returns_Error()
        {
            var result = AnimationIkSetupTool.Execute(new AnimationIkSetupParams
            {
                GameObjectName = "IKTestGO",
                Solver = "fabrik",
                ChainBones = new string[0],
                Target = "IKTarget"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void Missing_Gameobject_Returns_Error()
        {
            var result = AnimationIkSetupTool.Execute(new AnimationIkSetupParams
            {
                GameObjectName = "NonExistent",
                Solver = "fabrik",
                ChainBones = new[] { "Bone1", "Bone2" },
                Target = "IKTarget"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.NOT_FOUND, result.ErrorCode);
        }

        [Test]
        public void Limb_With_Three_Bones_Returns_Error()
        {
            var result = AnimationIkSetupTool.Execute(new AnimationIkSetupParams
            {
                GameObjectName = "IKTestGO",
                Solver = "limb",
                ChainBones = new[] { "Bone1", "Bone2", "Bone3" },
                Target = "IKTarget"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }
    }
}
