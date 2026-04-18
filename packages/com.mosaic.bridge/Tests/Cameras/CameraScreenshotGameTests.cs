using System;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Cameras;

namespace Mosaic.Bridge.Tests.Camera
{
    [TestFixture]
    public class CameraScreenshotGameTests
    {
        private GameObject _cameraGo;
        private GameObject _existingMainCamera;

        [SetUp]
        public void SetUp()
        {
            // Disable any existing MainCamera so our test camera is Camera.main
            _existingMainCamera = GameObject.FindWithTag("MainCamera");
            if (_existingMainCamera != null)
                _existingMainCamera.SetActive(false);

            _cameraGo = new GameObject("TestCamera_Game");
            _cameraGo.AddComponent<UnityEngine.Camera>();
            _cameraGo.tag = "MainCamera";
        }

        [TearDown]
        public void TearDown()
        {
            if (_cameraGo != null)
                UnityEngine.Object.DestroyImmediate(_cameraGo);

            // Restore existing MainCamera
            if (_existingMainCamera != null)
                _existingMainCamera.SetActive(true);
        }

        [Test]
        public void Execute_MainCamera_ReturnsValidPng()
        {
            var result = CameraScreenshotGameTool.Execute(new CameraScreenshotGameParams
            {
                IncludeBase64 = true
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("TestCamera_Game", result.Data.CameraName);
            Assert.IsNotNull(result.Data.FilePath);
            AssertPngHeader(result.Data.Base64Png);
        }

        [Test]
        public void Execute_ByInstanceId_ReturnsValidPng()
        {
            var cam = _cameraGo.GetComponent<UnityEngine.Camera>();
            var p = new CameraScreenshotGameParams
            {
                CameraInstanceId = cam.gameObject.GetInstanceID(),
                IncludeBase64 = true
            };

            var result = CameraScreenshotGameTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(cam.GetInstanceID(), result.Data.CameraInstanceId);
            Assert.IsNotNull(result.Data.FilePath);
            AssertPngHeader(result.Data.Base64Png);
        }

        [Test]
        public void Execute_InvalidInstanceId_ReturnsFail()
        {
            var p = new CameraScreenshotGameParams { CameraInstanceId = -99999 };

            var result = CameraScreenshotGameTool.Execute(p);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.NOT_FOUND, result.ErrorCode);
        }

        [Test]
        public void Execute_CustomResolution_RespectsValues()
        {
            var p = new CameraScreenshotGameParams { Width = 320, Height = 240 };

            var result = CameraScreenshotGameTool.Execute(p);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(320, result.Data.Width);
            Assert.AreEqual(240, result.Data.Height);
        }

        [Test]
        public void Execute_NoMainCamera_ReturnsFail()
        {
            // Remove our test camera
            UnityEngine.Object.DestroyImmediate(_cameraGo);
            _cameraGo = null;

            // Existing main camera is already disabled in SetUp
            // So Camera.main should be null now
            var result = CameraScreenshotGameTool.Execute(new CameraScreenshotGameParams());

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.NOT_FOUND, result.ErrorCode);
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
