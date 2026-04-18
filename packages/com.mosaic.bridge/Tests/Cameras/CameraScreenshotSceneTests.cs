using System;
using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Cameras;

namespace Mosaic.Bridge.Tests.Camera
{
    [TestFixture]
    public class CameraScreenshotSceneTests
    {
        [Test]
        public void Execute_DefaultResolution_ReturnsValidPng()
        {
            // SceneView.lastActiveSceneView may be null in batch mode
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                Assert.Ignore("No active Scene View available (batch mode)");
                return;
            }

            var result = CameraScreenshotSceneTool.Execute(new CameraScreenshotSceneParams
            {
                IncludeBase64 = true
            });

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(1920, result.Data.Width);
            Assert.AreEqual(1080, result.Data.Height);
            Assert.IsNotNull(result.Data.FilePath);
            AssertPngHeader(result.Data.Base64Png);
        }

        [Test]
        public void Execute_CustomResolution_RespectsValues()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                Assert.Ignore("No active Scene View available (batch mode)");
                return;
            }

            var p = new CameraScreenshotSceneParams { Width = 640, Height = 480, IncludeBase64 = true };
            var result = CameraScreenshotSceneTool.Execute(p);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(640, result.Data.Width);
            Assert.AreEqual(480, result.Data.Height);
            Assert.IsNotNull(result.Data.FilePath);
            AssertPngHeader(result.Data.Base64Png);
        }

        [Test]
        public void Execute_NullResolution_UsesDefaults()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                Assert.Ignore("No active Scene View available (batch mode)");
                return;
            }

            var p = new CameraScreenshotSceneParams { Width = null, Height = null };
            var result = CameraScreenshotSceneTool.Execute(p);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(CameraToolHelpers.DefaultWidth, result.Data.Width);
            Assert.AreEqual(CameraToolHelpers.DefaultHeight, result.Data.Height);
        }

        private static void AssertPngHeader(string base64)
        {
            Assert.IsNotNull(base64);
            Assert.IsNotEmpty(base64);
            var bytes = Convert.FromBase64String(base64);
            Assert.GreaterOrEqual(bytes.Length, 4);
            // PNG magic bytes: 0x89 0x50 0x4E 0x47
            Assert.AreEqual(0x89, bytes[0]);
            Assert.AreEqual(0x50, bytes[1]);
            Assert.AreEqual(0x4E, bytes[2]);
            Assert.AreEqual(0x47, bytes[3]);
        }
    }
}
