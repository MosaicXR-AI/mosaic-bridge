using System;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Cameras;

namespace Mosaic.Bridge.Tests.Camera
{
    [TestFixture]
    public class CameraScreenshotCameraTests
    {
        private GameObject _cameraGo;

        [SetUp]
        public void SetUp()
        {
            _cameraGo = new GameObject("TestCamera_Specific");
            _cameraGo.AddComponent<UnityEngine.Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_cameraGo != null)
                UnityEngine.Object.DestroyImmediate(_cameraGo);
        }

        [Test]
        public void Execute_ValidInstanceId_ReturnsValidPng()
        {
            var p = new CameraScreenshotCameraParams
            {
                InstanceId = _cameraGo.GetInstanceID(),
                IncludeBase64 = true
            };

            var result = CameraScreenshotCameraTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("TestCamera_Specific", result.Data.CameraName);
            Assert.IsNotNull(result.Data.FilePath);
            AssertPngHeader(result.Data.Base64Png);
        }

        [Test]
        public void Execute_CustomResolution_RespectsValues()
        {
            var p = new CameraScreenshotCameraParams
            {
                InstanceId = _cameraGo.GetInstanceID(),
                Width = 256,
                Height = 256
            };

            var result = CameraScreenshotCameraTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(256, result.Data.Width);
            Assert.AreEqual(256, result.Data.Height);
        }

        [Test]
        public void Execute_InvalidInstanceId_ReturnsFail()
        {
            var p = new CameraScreenshotCameraParams { InstanceId = -99999 };

            var result = CameraScreenshotCameraTool.Execute(p);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.NOT_FOUND, result.ErrorCode);
        }

        [Test]
        public void Execute_NonCameraObject_ReturnsFail()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "NotACamera";

            try
            {
                var p = new CameraScreenshotCameraParams { InstanceId = cube.GetInstanceID() };

                var result = CameraScreenshotCameraTool.Execute(p);

                Assert.IsFalse(result.Success);
                // Tool returns NOT_FOUND when the GO has no Camera component
                Assert.That(result.ErrorCode,
                    Is.EqualTo(ErrorCodes.NOT_FOUND).Or.EqualTo(ErrorCodes.INVALID_PARAM));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void Execute_DefaultResolution_Uses1920x1080()
        {
            var p = new CameraScreenshotCameraParams
            {
                InstanceId = _cameraGo.GetInstanceID()
            };

            var result = CameraScreenshotCameraTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1920, result.Data.Width);
            Assert.AreEqual(1080, result.Data.Height);
        }

        private static void AssertPngHeader(string base64)
        {
            Assert.IsNotNull(base64);
            Assert.IsNotEmpty(base64);
            var bytes = Convert.FromBase64String(base64);
            Assert.GreaterOrEqual(bytes.Length, 4);
            Assert.AreEqual(0x89, bytes[0]);
            Assert.AreEqual(0x50, bytes[1]);
            Assert.AreEqual(0x4E, bytes[2]);
            Assert.AreEqual(0x47, bytes[3]);
        }
    }
}
