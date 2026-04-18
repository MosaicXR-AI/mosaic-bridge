using NUnit.Framework;
using UnityEditor;
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
    }
}
