using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Measure;

namespace Mosaic.Bridge.Tests.Unit.Tools.Measure
{
    [TestFixture]
    [Category("Unit")]
    public class BoundsTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _created.Clear();
        }

        private GameObject MakeCube(string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            _created.Add(go);
            return go;
        }

        [Test]
        public void UnitCube_Aabb_Returns1x1x1()
        {
            var go = MakeCube("Bounds_UnitCube", Vector3.zero, Vector3.one);

            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = go.name,
                Mode = "aabb",
                IncludeChildren = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1f, result.Data.Size[0], 1e-4f);
            Assert.AreEqual(1f, result.Data.Size[1], 1e-4f);
            Assert.AreEqual(1f, result.Data.Size[2], 1e-4f);
            Assert.AreEqual("aabb", result.Data.Mode);
            Assert.AreEqual("meters", result.Data.Unit);
            Assert.AreEqual(-1, result.Data.AnnotationId);
        }

        [Test]
        public void UnitCube_Volume_IsSizeProduct()
        {
            var go = MakeCube("Bounds_Vol", Vector3.zero, new Vector3(2f, 3f, 4f));

            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = go.name,
                Mode = "aabb",
                IncludeChildren = false
            });

            Assert.IsTrue(result.Success, result.Error);
            var s = result.Data.Size;
            float expected = s[0] * s[1] * s[2];
            Assert.AreEqual(expected, result.Data.Volume, 1e-3f);
            // Physical: 2*3*4 = 24
            Assert.AreEqual(24f, result.Data.Volume, 1e-3f);
        }

        [Test]
        public void UnitCube_SurfaceArea_IsSixFaceFormula()
        {
            var go = MakeCube("Bounds_SA", Vector3.zero, new Vector3(2f, 3f, 4f));

            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = go.name,
                Mode = "aabb",
                IncludeChildren = false
            });

            Assert.IsTrue(result.Success, result.Error);
            var s = result.Data.Size;
            float expected = 2f * (s[0] * s[1] + s[1] * s[2] + s[0] * s[2]);
            Assert.AreEqual(expected, result.Data.SurfaceArea, 1e-3f);
            // Physical: 2*(6+12+8) = 52
            Assert.AreEqual(52f, result.Data.SurfaceArea, 1e-3f);
        }

        [Test]
        public void UnitCube_Diagonal_IsSqrt3()
        {
            var go = MakeCube("Bounds_Diag", Vector3.zero, Vector3.one);

            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = go.name,
                Mode = "aabb",
                IncludeChildren = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(Mathf.Sqrt(3f), result.Data.DiagonalLength, 1e-4f);
        }

        [Test]
        public void IncludeChildren_MergesChildBounds()
        {
            var parent = new GameObject("Bounds_Parent");
            _created.Add(parent);

            var child1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child1.transform.SetParent(parent.transform);
            child1.transform.position = new Vector3(-2f, 0f, 0f);

            var child2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child2.transform.SetParent(parent.transform);
            child2.transform.position = new Vector3(2f, 0f, 0f);

            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = parent.name,
                Mode = "aabb",
                IncludeChildren = true
            });

            Assert.IsTrue(result.Success, result.Error);
            // Two unit cubes centered at x = -2 and x = +2 (each 1 wide): combined X size = 5
            Assert.AreEqual(5f, result.Data.Size[0], 1e-3f);
            Assert.AreEqual(1f, result.Data.Size[1], 1e-3f);
            Assert.AreEqual(1f, result.Data.Size[2], 1e-3f);
        }

        [Test]
        public void InvalidMode_ReturnsError()
        {
            var go = MakeCube("Bounds_InvalidMode", Vector3.zero, Vector3.one);

            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = go.name,
                Mode = "hyperbola"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void MissingGameObject_ReturnsError()
        {
            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = "DoesNotExist_" + System.Guid.NewGuid(),
                Mode = "aabb"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void MissingGameObjectName_ReturnsError()
        {
            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = "",
                Mode = "aabb"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void CreateVisual_CreatesWireframeGameObject()
        {
            var go = MakeCube("Bounds_Visual", Vector3.zero, Vector3.one);

            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = go.name,
                Mode = "aabb",
                CreateVisual = true,
                VisualColor = new[] { 1f, 0f, 0f, 1f }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreNotEqual(-1, result.Data.AnnotationId);

            var visual = Resources.EntityIdToObject(result.Data.AnnotationId) as GameObject;
            Assert.IsNotNull(visual, "Wireframe visual GameObject should exist");
            _created.Add(visual);

            var lr = visual.GetComponent<LineRenderer>();
            Assert.IsNotNull(lr, "Wireframe visual should have a LineRenderer");
            Assert.GreaterOrEqual(lr.positionCount, 16, "Wireframe should trace all 12 box edges");
        }

        [Test]
        public void UnitConversion_Centimeters_Scales100x()
        {
            var go = MakeCube("Bounds_Cm", Vector3.zero, Vector3.one);

            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = go.name,
                Mode = "aabb",
                Unit = "centimeters",
                IncludeChildren = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(100f, result.Data.Size[0], 1e-2f);
            Assert.AreEqual("centimeters", result.Data.Unit);
        }

        [Test]
        public void RendererMode_OnCubeReturnsBounds()
        {
            var go = MakeCube("Bounds_Renderer", Vector3.zero, Vector3.one);

            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = go.name,
                Mode = "renderer",
                IncludeChildren = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("renderer", result.Data.Mode);
            Assert.AreEqual(1f, result.Data.Size[0], 1e-4f);
        }

        [Test]
        public void MeshMode_OnCubeReturnsBounds()
        {
            var go = MakeCube("Bounds_Mesh", Vector3.zero, Vector3.one);

            var result = MeasureBoundsTool.Execute(new MeasureBoundsParams
            {
                GameObjectName = go.name,
                Mode = "mesh",
                IncludeChildren = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("mesh", result.Data.Mode);
            Assert.AreEqual(1f, result.Data.Size[0], 1e-3f);
            Assert.AreEqual(1f, result.Data.Size[1], 1e-3f);
            Assert.AreEqual(1f, result.Data.Size[2], 1e-3f);
        }
    }
}
