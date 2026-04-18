using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Measure;

namespace Mosaic.Bridge.Tests.Unit.Tools.Measure
{
    /// <summary>
    /// Unit tests for the measure/angle tool (Story 33-2).
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("Measure")]
    public class AngleTests
    {
        const float Eps = 1e-3f;

        [Test]
        public void RightAngle_FromPerpendicularRays_Returns90Degrees()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f, 0f },
                PointA      = new[] { 1f, 0f, 0f },
                PointB      = new[] { 0f, 1f, 0f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("degrees", result.Data.Unit);
            Assert.AreEqual(90f, result.Data.Angle, Eps);
            Assert.AreEqual(-1, result.Data.AnnotationId);
        }

        [Test]
        public void StraightLine_FromOppositeRays_Returns180Degrees()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f, 0f },
                PointA      = new[] { 1f, 0f, 0f },
                PointB      = new[] { -1f, 0f, 0f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(180f, result.Data.Angle, Eps);
        }

        [Test]
        public void ParallelRays_ReturnsZeroAngle()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f, 0f },
                PointA      = new[] { 1f, 0f, 0f },
                PointB      = new[] { 2f, 0f, 0f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0f, result.Data.Angle, Eps);
        }

        [Test]
        public void Radians_ReturnsAngleInRadians()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f, 0f },
                PointA      = new[] { 1f, 0f, 0f },
                PointB      = new[] { 0f, 1f, 0f },
                Unit        = "radians",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("radians", result.Data.Unit);
            Assert.AreEqual(Mathf.PI / 2f, result.Data.Angle, Eps);
        }

        [Test]
        public void Degrees_AndRadians_AreConsistent()
        {
            var deg = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f, 0f },
                PointA      = new[] { 1f, 0f, 0f },
                PointB      = new[] { 1f, 1f, 0f },
                Unit        = "degrees",
            });
            var rad = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f, 0f },
                PointA      = new[] { 1f, 0f, 0f },
                PointB      = new[] { 1f, 1f, 0f },
                Unit        = "radians",
            });

            Assert.IsTrue(deg.Success);
            Assert.IsTrue(rad.Success);
            Assert.AreEqual(45f, deg.Data.Angle, Eps);
            Assert.AreEqual(deg.Data.Angle * Mathf.Deg2Rad, rad.Data.Angle, Eps);
        }

        [Test]
        public void MissingVertex_ReturnsError()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                PointA = new[] { 1f, 0f, 0f },
                PointB = new[] { 0f, 1f, 0f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void MissingPointA_ReturnsError()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f, 0f },
                PointB      = new[] { 0f, 1f, 0f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void CoincidentPoint_ReturnsError()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f, 0f },
                PointA      = new[] { 0f, 0f, 0f },
                PointB      = new[] { 0f, 1f, 0f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void InvalidUnit_ReturnsError()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f, 0f },
                PointA      = new[] { 1f, 0f, 0f },
                PointB      = new[] { 0f, 1f, 0f },
                Unit        = "gradians",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void InvalidCoordinateLength_ReturnsError()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f },
                PointA      = new[] { 1f, 0f, 0f },
                PointB      = new[] { 0f, 1f, 0f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void GameObjectName_NotFound_ReturnsError()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexGameObject = "__nonexistent_vertex_go__",
                PointA           = new[] { 1f, 0f, 0f },
                PointB           = new[] { 0f, 1f, 0f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void GameObjectResolution_UsesWorldPosition()
        {
            var vGO = new GameObject("__AngleTest_Vertex");
            var aGO = new GameObject("__AngleTest_A");
            var bGO = new GameObject("__AngleTest_B");
            try
            {
                vGO.transform.position = new Vector3(1, 2, 3);
                aGO.transform.position = new Vector3(2, 2, 3); // +X from vertex
                bGO.transform.position = new Vector3(1, 3, 3); // +Y from vertex

                var result = MeasureAngleTool.Execute(new MeasureAngleParams
                {
                    VertexGameObject = vGO.name,
                    GameObjectA      = aGO.name,
                    GameObjectB      = bGO.name,
                });

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual(90f, result.Data.Angle, Eps);
                Assert.AreEqual(1f, result.Data.Vertex[0], Eps);
                Assert.AreEqual(2f, result.Data.Vertex[1], Eps);
                Assert.AreEqual(3f, result.Data.Vertex[2], Eps);
            }
            finally
            {
                Object.DestroyImmediate(vGO);
                Object.DestroyImmediate(aGO);
                Object.DestroyImmediate(bGO);
            }
        }

        [Test]
        public void CreateVisual_CreatesArcAnnotation()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint  = new[] { 0f, 0f, 0f },
                PointA       = new[] { 1f, 0f, 0f },
                PointB       = new[] { 0f, 1f, 0f },
                CreateVisual = true,
                Name         = "__AngleTest_Visual",
                ArcRadius    = 0.75f,
                VisualColor  = new[] { 1f, 0f, 0f, 1f },
            });

            GameObject go = null;
            try
            {
                Assert.IsTrue(result.Success, result.Error);
                Assert.AreNotEqual(-1, result.Data.AnnotationId);
                Assert.AreEqual("__AngleTest_Visual", result.Data.AnnotationName);

                go = GameObject.Find("__AngleTest_Visual");
                Assert.IsNotNull(go, "Annotation GameObject should exist in scene");

                var line = go.GetComponent<LineRenderer>();
                Assert.IsNotNull(line, "Annotation should have a LineRenderer");
                Assert.GreaterOrEqual(line.positionCount, 2);

                var label = go.transform.Find("Label");
                Assert.IsNotNull(label, "Annotation should have a child Label");
                var text = label.GetComponent<TextMesh>();
                Assert.IsNotNull(text);
                StringAssert.Contains("90", text.text);
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CreateVisual_InvalidArcRadius_ReturnsError()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint  = new[] { 0f, 0f, 0f },
                PointA       = new[] { 1f, 0f, 0f },
                PointB       = new[] { 0f, 1f, 0f },
                CreateVisual = true,
                ArcRadius    = 0f,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void RayVectors_AreNormalized()
        {
            var result = MeasureAngleTool.Execute(new MeasureAngleParams
            {
                VertexPoint = new[] { 0f, 0f, 0f },
                PointA      = new[] { 5f, 0f, 0f },
                PointB      = new[] { 0f, 10f, 0f },
            });

            Assert.IsTrue(result.Success, result.Error);
            var rayA = new Vector3(result.Data.RayA[0], result.Data.RayA[1], result.Data.RayA[2]);
            var rayB = new Vector3(result.Data.RayB[0], result.Data.RayB[1], result.Data.RayB[2]);
            Assert.AreEqual(1f, rayA.magnitude, Eps);
            Assert.AreEqual(1f, rayB.magnitude, Eps);
        }
    }
}
