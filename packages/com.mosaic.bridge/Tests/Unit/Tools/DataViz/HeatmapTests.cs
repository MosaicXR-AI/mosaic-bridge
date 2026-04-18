using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.DataViz;

namespace Mosaic.Bridge.Tests.Unit.Tools.DataViz
{
    /// <summary>
    /// Unit tests for the data/heatmap tool (Story 34-1).
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("DataViz")]
    public class HeatmapTests
    {
        const string TestSavePath = "Assets/Generated/DataVizTests/";
        GameObject _target;

        [SetUp]
        public void SetUp()
        {
            // Use a Unity primitive plane — it has a MeshFilter with proper UVs.
            _target = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _target.name = "__Heatmap_Plane_" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
        }

        [TearDown]
        public void TearDown()
        {
            if (_target != null) Object.DestroyImmediate(_target);

            // Clean generated test assets so reruns stay hermetic.
            if (AssetDatabase.IsValidFolder(TestSavePath.TrimEnd('/')))
            {
                AssetDatabase.DeleteAsset(TestSavePath.TrimEnd('/'));
                AssetDatabase.Refresh();
            }
        }

        static List<DataHeatmapParams.DataPoint> FourCornerPoints()
        {
            return new List<DataHeatmapParams.DataPoint>
            {
                new DataHeatmapParams.DataPoint { Position = new[] { -5f, 0f, -5f }, Value = 0f  },
                new DataHeatmapParams.DataPoint { Position = new[] {  5f, 0f, -5f }, Value = 25f },
                new DataHeatmapParams.DataPoint { Position = new[] { -5f, 0f,  5f }, Value = 50f },
                new DataHeatmapParams.DataPoint { Position = new[] {  5f, 0f,  5f }, Value = 100f},
            };
        }

        DataHeatmapParams MakeBasicParams() => new DataHeatmapParams
        {
            TargetObject  = _target.name,
            DataPoints    = FourCornerPoints(),
            ColorGradient = "thermal",
            Interpolation = "idw",
            Resolution    = 32,
            SavePath      = TestSavePath,
            Name          = "Test",
        };

        [Test]
        public void BasicHeatmap_CreatesTextureAndMaterial()
        {
            var result = DataHeatmapTool.Execute(MakeBasicParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.TexturePath));
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.MaterialPath));

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(result.Data.TexturePath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(result.Data.MaterialPath);
            Assert.IsNotNull(tex, "Baked texture should be importable as Texture2D");
            Assert.IsNotNull(mat, "Material asset should be loadable");
        }

        [Test]
        public void EmptyDataPoints_ReturnsError()
        {
            var p = MakeBasicParams();
            p.DataPoints = new List<DataHeatmapParams.DataPoint>();
            var result = DataHeatmapTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void NullDataPoints_ReturnsError()
        {
            var p = MakeBasicParams();
            p.DataPoints = null;
            var result = DataHeatmapTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void MissingTargetObject_ReturnsError()
        {
            var p = MakeBasicParams();
            p.TargetObject = null;
            var result = DataHeatmapTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void NonExistentTargetObject_ReturnsNotFound()
        {
            var p = MakeBasicParams();
            p.TargetObject = "__no_such_object__";
            var result = DataHeatmapTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [TestCase("thermal")]
        [TestCase("viridis")]
        [TestCase("jet")]
        [TestCase("coolwarm")]
        [TestCase("grayscale")]
        public void EachGradientMode_ProducesValidTexture(string gradient)
        {
            var p = MakeBasicParams();
            p.ColorGradient = gradient;
            p.Name = "Test_" + gradient;

            var result = DataHeatmapTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(result.Data.TexturePath);
            Assert.IsNotNull(tex, $"Texture should load for gradient {gradient}");
        }

        [TestCase("nearest")]
        [TestCase("linear")]
        [TestCase("idw")]
        public void EachInterpolationMode_ProducesValidTexture(string interp)
        {
            var p = MakeBasicParams();
            p.Interpolation = interp;
            p.Name = "Test_" + interp;

            var result = DataHeatmapTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(result.Data.TexturePath));
        }

        [Test]
        public void InvalidGradient_ReturnsError()
        {
            var p = MakeBasicParams();
            p.ColorGradient = "bogus";
            var result = DataHeatmapTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void InvalidInterpolation_ReturnsError()
        {
            var p = MakeBasicParams();
            p.Interpolation = "quadratic";
            var result = DataHeatmapTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ShowLegend_CreatesLegendGameObject()
        {
            var p = MakeBasicParams();
            p.ShowLegend = true;

            var result = DataHeatmapTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.LegendGameObject));
            var legend = GameObject.Find(result.Data.LegendGameObject);
            Assert.IsNotNull(legend, "Legend GameObject should exist in scene");
            Assert.AreEqual(_target.transform, legend.transform.parent);
        }

        [Test]
        public void ShowLegend_False_NoLegendCreated()
        {
            var p = MakeBasicParams();
            p.ShowLegend = false;

            var result = DataHeatmapTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(string.IsNullOrEmpty(result.Data.LegendGameObject));
        }

        [Test]
        public void CustomValueMinMax_ClampsAndIsReported()
        {
            var p = MakeBasicParams();
            p.ValueMin = 10f;
            p.ValueMax = 40f;

            var result = DataHeatmapTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(10f, result.Data.ValueMin, 1e-4f);
            Assert.AreEqual(40f, result.Data.ValueMax, 1e-4f);
        }

        [Test]
        public void AutoValueRange_UsesDataPointMinMax()
        {
            var result = DataHeatmapTool.Execute(MakeBasicParams());
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0f,   result.Data.ValueMin, 1e-4f);
            Assert.AreEqual(100f, result.Data.ValueMax, 1e-4f);
        }

        [Test]
        public void InvalidResolution_ReturnsError()
        {
            var p = MakeBasicParams();
            p.Resolution = 2; // below min
            var result = DataHeatmapTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("OUT_OF_RANGE", result.ErrorCode);
        }

        [Test]
        public void InvalidSavePath_ReturnsError()
        {
            var p = MakeBasicParams();
            p.SavePath = "SomeOtherRoot/";
            var result = DataHeatmapTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void TargetWithoutMeshFilter_ReturnsError()
        {
            var empty = new GameObject("__Heatmap_NoMesh_" + System.Guid.NewGuid().ToString("N").Substring(0, 6));
            try
            {
                var p = MakeBasicParams();
                p.TargetObject = empty.name;
                var result = DataHeatmapTool.Execute(p);
                Assert.IsFalse(result.Success);
                Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }
    }
}
