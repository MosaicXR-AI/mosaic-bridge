using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Core.Pipeline;
using Mosaic.Bridge.Core.Pipeline.Capture;

namespace Mosaic.Bridge.Tests.Pipeline
{
    [TestFixture]
    public class SceneCaptureServiceTests
    {
        private GameObject _testObj;
        private SceneCaptureService _service;

        [SetUp]
        public void SetUp()
        {
            _testObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _testObj.name = "CaptureTestCube";
            _testObj.transform.position = new Vector3(0, 1, 0);
            _service = new SceneCaptureService();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testObj != null) UnityEngine.Object.DestroyImmediate(_testObj);
        }

        [Test]
        public void CaptureAroundTarget_DefaultAngles_Returns4Screenshots()
        {
            var settings = new CaptureSettings();

            var result = _service.CaptureAroundTarget(_testObj, settings);

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("front", result[0].AngleLabel);
            Assert.AreEqual("right", result[1].AngleLabel);
            Assert.AreEqual("top", result[2].AngleLabel);
            Assert.AreEqual("perspective", result[3].AngleLabel);
        }

        [Test]
        public void CaptureAroundTarget_CustomAngles_ReturnsCorrectCount()
        {
            var settings = new CaptureSettings
            {
                Angles = new[] { "front", "back" }
            };

            var result = _service.CaptureAroundTarget(_testObj, settings);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("front", result[0].AngleLabel);
            Assert.AreEqual("back", result[1].AngleLabel);
        }

        [Test]
        public void CaptureAroundTarget_NullTarget_ReturnsEmptyList()
        {
            var settings = new CaptureSettings();

            var result = _service.CaptureAroundTarget(null, settings);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void CaptureAroundTarget_NoRenderer_UsesDefaultBounds()
        {
            var emptyGo = new GameObject("EmptyCapturTarget");
            emptyGo.transform.position = new Vector3(3, 0, 0);

            try
            {
                var settings = new CaptureSettings
                {
                    Angles = new[] { "front" }
                };

                var result = _service.CaptureAroundTarget(emptyGo, settings);

                Assert.AreEqual(1, result.Count);
                Assert.AreEqual("front", result[0].AngleLabel);
                Assert.IsNotNull(result[0].Base64Png);
                Assert.Greater(result[0].Base64Png.Length, 0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(emptyGo);
            }
        }

        [Test]
        public void CaptureAroundTarget_ValidBase64Png_DecodesToPngHeader()
        {
            var settings = new CaptureSettings
            {
                Angles = new[] { "front" },
                Resolution = 64
            };

            var result = _service.CaptureAroundTarget(_testObj, settings);

            Assert.AreEqual(1, result.Count);
            var bytes = Convert.FromBase64String(result[0].Base64Png);
            Assert.GreaterOrEqual(bytes.Length, 4);

            // PNG magic bytes: 0x89 0x50 0x4E 0x47
            Assert.AreEqual(0x89, bytes[0]);
            Assert.AreEqual(0x50, bytes[1]);
            Assert.AreEqual(0x4E, bytes[2]);
            Assert.AreEqual(0x47, bytes[3]);
        }

        [Test]
        public void CaptureAroundTarget_CleanupNoLeakedObjects()
        {
            var settings = new CaptureSettings();

            _service.CaptureAroundTarget(_testObj, settings);

            var leaked = GameObject.Find("__MosaicCapture");
            Assert.IsNull(leaked, "Temporary capture camera was not cleaned up");
        }
    }
}
