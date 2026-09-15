using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Renderers;

namespace Mosaic.Bridge.Tests.Unit.Tools.Renderers
{
    // O4 §4.8: renderer/line + renderer/trail — laser sights, path previews, grappling hooks,
    // sword trails. component/set_property cannot set Vector3[]/Gradient, so these needed a
    // dedicated route.
    [TestFixture]
    [Category("Renderers")]
    public class RendererToolTests
    {
        private GameObject _testGo;

        [SetUp]
        public void SetUp()
        {
            _testGo = new GameObject("RendererTestObject");
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null) Object.DestroyImmediate(_testGo);
        }

        // ── renderer/line ────────────────────────────────────────────────────

        [Test]
        public void Line_AddsWithPositions()
        {
            var result = RendererLineTool.Execute(new RendererLineParams
            {
                Name = "RendererTestObject",
                Positions = new[] { 0f, 0f, 0f, 1f, 2f, 3f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.ComponentAdded);
            Assert.AreEqual(2, result.Data.PositionCount);

            var lr = _testGo.GetComponent<LineRenderer>();
            Assert.AreEqual(new Vector3(1f, 2f, 3f), lr.GetPosition(1));
        }

        [Test]
        public void Line_WidthCurveAndColors_Apply()
        {
            var result = RendererLineTool.Execute(new RendererLineParams
            {
                Name = "RendererTestObject",
                Positions = new[] { 0f, 0f, 0f, 5f, 0f, 0f },
                WidthCurveTimes = new[] { 0f, 1f },
                WidthCurveValues = new[] { 0.1f, 0.5f },
                StartColor = new[] { 1f, 0f, 0f },
                EndColor = new[] { 0f, 0f, 1f },
            });

            Assert.IsTrue(result.Success, result.Error);
            var lr = _testGo.GetComponent<LineRenderer>();
            Assert.AreEqual(0.1f, lr.widthCurve.Evaluate(0f), 0.001f);
            Assert.AreEqual(0.5f, lr.widthCurve.Evaluate(1f), 0.001f);
            Assert.AreEqual(Color.red, lr.startColor);
        }

        [Test]
        public void Line_TooFewPositions_ReturnsInvalidParam()
        {
            var result = RendererLineTool.Execute(new RendererLineParams
            {
                Name = "RendererTestObject",
                Positions = new[] { 0f, 0f, 0f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Line_UnknownAlignment_ReturnsInvalidParam()
        {
            var result = RendererLineTool.Execute(new RendererLineParams
            {
                Name = "RendererTestObject",
                Positions = new[] { 0f, 0f, 0f, 1f, 1f, 1f },
                Alignment = "Diagonal",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Line_ReconfigureExisting_DoesNotAddSecondComponent()
        {
            RendererLineTool.Execute(new RendererLineParams
            {
                Name = "RendererTestObject", Positions = new[] { 0f, 0f, 0f, 1f, 1f, 1f },
            });

            var result = RendererLineTool.Execute(new RendererLineParams
            {
                Name = "RendererTestObject", Positions = new[] { 0f, 0f, 0f, 2f, 2f, 2f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(result.Data.ComponentAdded);
            Assert.AreEqual(1, _testGo.GetComponents<LineRenderer>().Length);
        }

        // ── renderer/trail ───────────────────────────────────────────────────

        [Test]
        public void Trail_AddsAndConfigures()
        {
            var result = RendererTrailTool.Execute(new RendererTrailParams
            {
                Name = "RendererTestObject",
                Time = 2f, MinVertexDistance = 0.05f, Emitting = true,
                StartWidth = 0.2f, EndWidth = 0.01f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.ComponentAdded);
            Assert.AreEqual(2f, result.Data.Time, 0.001f);

            var tr = _testGo.GetComponent<TrailRenderer>();
            Assert.AreEqual(0.05f, tr.minVertexDistance, 0.001f);
            Assert.IsTrue(tr.emitting);
        }

        [Test]
        public void Trail_Autodestruct_Applies()
        {
            var result = RendererTrailTool.Execute(new RendererTrailParams
            {
                Name = "RendererTestObject", Autodestruct = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.Autodestruct);
            Assert.IsTrue(_testGo.GetComponent<TrailRenderer>().autodestruct);
        }

        [Test]
        public void Trail_NotFound_ReturnsNotFound()
        {
            var result = RendererTrailTool.Execute(new RendererTrailParams { Name = "DoesNotExist_Mosaic" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}
