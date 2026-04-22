using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Measure;

namespace Mosaic.Bridge.Tests.Unit.Tools.Measure
{
    [TestFixture]
    [Category("Unit")]
    public class DistanceTests
    {
        private GameObject _goA;
        private GameObject _goB;
        private GameObject _annotation;

        [TearDown]
        public void TearDown()
        {
            if (_goA != null) Object.DestroyImmediate(_goA);
            if (_goB != null) Object.DestroyImmediate(_goB);
            if (_annotation != null) Object.DestroyImmediate(_annotation);
            _goA = null;
            _goB = null;
            _annotation = null;
        }

        [Test]
        public void ExplicitPoints_ReturnsCorrectDistance()
        {
            var result = MeasureDistanceTool.Execute(new MeasureDistanceParams
            {
                PointA = new float[] { 0f, 0f, 0f },
                PointB = new float[] { 3f, 4f, 0f }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(5f, result.Data.Distance, 1e-4f);
            Assert.AreEqual("meters", result.Data.Unit);
            Assert.AreEqual(-1, result.Data.AnnotationId);
        }

        [Test]
        public void UnitConversion_MetersVsMillimeters()
        {
            var meters = MeasureDistanceTool.Execute(new MeasureDistanceParams
            {
                PointA = new float[] { 0f, 0f, 0f },
                PointB = new float[] { 1f, 0f, 0f },
                Unit   = "meters"
            });
            var mm = MeasureDistanceTool.Execute(new MeasureDistanceParams
            {
                PointA = new float[] { 0f, 0f, 0f },
                PointB = new float[] { 1f, 0f, 0f },
                Unit   = "millimeters"
            });

            Assert.IsTrue(meters.Success);
            Assert.IsTrue(mm.Success);
            Assert.AreEqual(1f, meters.Data.Distance, 1e-4f);
            Assert.AreEqual(1000f, mm.Data.Distance, 1e-2f);
        }

        [Test]
        public void GameObjectBased_UsesTransformPositions()
        {
            _goA = new GameObject("DistTestA");
            _goB = new GameObject("DistTestB");
            _goA.transform.position = new Vector3(0f, 0f, 0f);
            _goB.transform.position = new Vector3(0f, 0f, 10f);

            var result = MeasureDistanceTool.Execute(new MeasureDistanceParams
            {
                GameObjectA = "DistTestA",
                GameObjectB = "DistTestB"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(10f, result.Data.Distance, 1e-4f);
        }

        [Test]
        public void MissingBothPoints_ReturnsError()
        {
            var result = MeasureDistanceTool.Execute(new MeasureDistanceParams());
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void CreateVisual_CreatesGameObjectWithLineRenderer()
        {
            var result = MeasureDistanceTool.Execute(new MeasureDistanceParams
            {
                PointA       = new float[] { 0f, 0f, 0f },
                PointB       = new float[] { 1f, 0f, 0f },
                CreateVisual = true,
                Name         = "TestMeasureAnnotation"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreNotEqual(-1, result.Data.AnnotationId);
            Assert.AreEqual("TestMeasureAnnotation", result.Data.AnnotationName);

            _annotation = Resources.EntityIdToObject(result.Data.AnnotationId) as GameObject;
            Assert.IsNotNull(_annotation);
            Assert.IsNotNull(_annotation.GetComponent<LineRenderer>());
            // Label child should exist
            Assert.AreEqual(1, _annotation.transform.childCount);
        }

        [Test]
        public void InvalidUnit_ReturnsError()
        {
            var result = MeasureDistanceTool.Execute(new MeasureDistanceParams
            {
                PointA = new float[] { 0f, 0f, 0f },
                PointB = new float[] { 1f, 0f, 0f },
                Unit   = "parsecs"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void InvalidMode_ReturnsError()
        {
            var result = MeasureDistanceTool.Execute(new MeasureDistanceParams
            {
                PointA = new float[] { 0f, 0f, 0f },
                PointB = new float[] { 1f, 0f, 0f },
                Mode   = "teleport"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
