using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Cameras;

namespace Mosaic.Bridge.Tests.Camera
{
    [TestFixture]
    public class CameraInfoTests
    {
        private GameObject _cameraGo;
        private GameObject _existingMainCamera;

        [SetUp]
        public void SetUp()
        {
            // Disable any existing MainCamera so our test camera is the main one
            _existingMainCamera = GameObject.FindWithTag("MainCamera");
            if (_existingMainCamera != null)
                _existingMainCamera.SetActive(false);

            _cameraGo = new GameObject("TestCamera_Info");
            var cam = _cameraGo.AddComponent<UnityEngine.Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 500f;
            cam.depth = 2f;
            _cameraGo.tag = "MainCamera";
        }

        [TearDown]
        public void TearDown()
        {
            if (_cameraGo != null)
                UnityEngine.Object.DestroyImmediate(_cameraGo);

            if (_existingMainCamera != null)
                _existingMainCamera.SetActive(true);
        }

        [Test]
        public void Execute_AllCameras_ReturnsAtLeastOne()
        {
            var result = CameraInfoTool.Execute(new CameraInfoParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.GreaterOrEqual(result.Data.Cameras.Length, 1);
        }

        [Test]
        public void Execute_AllCameras_ContainsTestCamera()
        {
            var result = CameraInfoTool.Execute(new CameraInfoParams());

            Assert.IsTrue(result.Success, result.Error);
            bool found = false;
            foreach (var entry in result.Data.Cameras)
            {
                if (entry.Name == "TestCamera_Info")
                {
                    found = true;
                    Assert.AreEqual(75f, entry.FieldOfView, 0.01f);
                    Assert.AreEqual(0.5f, entry.NearClipPlane, 0.01f);
                    Assert.AreEqual(500f, entry.FarClipPlane, 0.01f);
                    Assert.AreEqual(2f, entry.Depth, 0.01f);
                    Assert.IsTrue(entry.IsMainCamera);
                    Assert.IsNotNull(entry.Position);
                    Assert.AreEqual(3, entry.Position.Length);
                    Assert.IsNotNull(entry.Rotation);
                    Assert.AreEqual(3, entry.Rotation.Length);
                    break;
                }
            }
            Assert.IsTrue(found, "TestCamera_Info not found in camera list");
        }

        [Test]
        public void Execute_ByInstanceId_ReturnsSingleCamera()
        {
            var cam = _cameraGo.GetComponent<UnityEngine.Camera>();
            var p = new CameraInfoParams { InstanceId = cam.gameObject.GetInstanceID() };

            var result = CameraInfoTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Cameras.Length);
            Assert.AreEqual("TestCamera_Info", result.Data.Cameras[0].Name);
        }

        [Test]
        public void Execute_InvalidInstanceId_ReturnsFail()
        {
            var p = new CameraInfoParams { InstanceId = -99999 };

            var result = CameraInfoTool.Execute(p);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.NOT_FOUND, result.ErrorCode);
        }

        [Test]
        public void Execute_NonCameraInstanceId_ReturnsFail()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "NotACameraForInfo";

            try
            {
                var p = new CameraInfoParams { InstanceId = cube.GetInstanceID() };

                var result = CameraInfoTool.Execute(p);

                Assert.IsFalse(result.Success);
                Assert.That(result.ErrorCode,
                    Is.EqualTo(ErrorCodes.NOT_FOUND).Or.EqualTo(ErrorCodes.INVALID_PARAM));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void Execute_CameraHierarchyPath_IsPopulated()
        {
            var parent = new GameObject("ParentObj");
            _cameraGo.transform.SetParent(parent.transform);

            try
            {
                var cam = _cameraGo.GetComponent<UnityEngine.Camera>();
                var p = new CameraInfoParams { InstanceId = cam.gameObject.GetInstanceID() };

                var result = CameraInfoTool.Execute(p);

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual("ParentObj/TestCamera_Info", result.Data.Cameras[0].HierarchyPath);
            }
            finally
            {
                _cameraGo.transform.SetParent(null);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Execute_InvalidInstanceId_ForNonExistentObject_ReturnsFail()
        {
            // Instead of destroying all cameras (which causes targetScene assertion),
            // test the error path with an invalid instance ID
            var p = new CameraInfoParams { InstanceId = -12345 };

            var result = CameraInfoTool.Execute(p);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.NOT_FOUND, result.ErrorCode);
        }
    }
}
