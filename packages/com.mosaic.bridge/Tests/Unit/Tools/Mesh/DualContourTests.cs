using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.AdvancedMesh;

namespace Mosaic.Bridge.Tests.Unit.Tools.Mesh
{
    [TestFixture]
    [Category("Unit")]
    public class DualContourTests
    {
        private const string TestSavePath = "Assets/MosaicTestTemp/DualContour/";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder("Assets/MosaicTestTemp"))
                AssetDatabase.DeleteAsset("Assets/MosaicTestTemp");
        }

        [Test]
        public void Sphere_Sdf_ProducesValidMesh()
        {
            var result = MeshDualContourTool.Execute(new MeshDualContourParams
            {
                SdfFunction = "sphere",
                SdfRadius = 3f,
                Resolution = 16,
                BoundsMin = new[] { -5f, -5f, -5f },
                BoundsMax = new[] { 5f, 5f, 5f },
                SavePath = TestSavePath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.Greater(result.Data.VertexCount, 0);
            Assert.Greater(result.Data.TriangleCount, 0);
            Assert.AreEqual("sphere", result.Data.SdfFunction);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(result.Data.MeshPath));
        }

        [Test]
        public void Box_Sdf_ProducesBoxMesh()
        {
            var result = MeshDualContourTool.Execute(new MeshDualContourParams
            {
                SdfFunction = "box",
                SdfSize = new[] { 2f, 2f, 2f },
                Resolution = 16,
                BoundsMin = new[] { -5f, -5f, -5f },
                BoundsMax = new[] { 5f, 5f, 5f },
                SavePath = TestSavePath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.Greater(result.Data.VertexCount, 0);
        }

        [Test]
        public void Resolution_Clamped_To_Safe_Range()
        {
            var result = MeshDualContourTool.Execute(new MeshDualContourParams
            {
                SdfFunction = "sphere",
                SdfRadius = 3f,
                Resolution = 999,
                BoundsMin = new[] { -5f, -5f, -5f },
                BoundsMax = new[] { 5f, 5f, 5f },
                SavePath = TestSavePath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.LessOrEqual(result.Data.Resolution, 64);
        }

        [Test]
        public void Invalid_Sdf_Returns_Error()
        {
            var result = MeshDualContourTool.Execute(new MeshDualContourParams
            {
                SdfFunction = "invalid_function",
                Resolution = 16
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void Invalid_Bounds_Returns_Error()
        {
            var result = MeshDualContourTool.Execute(new MeshDualContourParams
            {
                SdfFunction = "sphere",
                Resolution = 16,
                BoundsMin = new[] { 5f, 5f, 5f },
                BoundsMax = new[] { -5f, -5f, -5f }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void Torus_Sdf_ProducesValidMesh()
        {
            var result = MeshDualContourTool.Execute(new MeshDualContourParams
            {
                SdfFunction = "torus",
                SdfRadius = 3f,
                Resolution = 16,
                BoundsMin = new[] { -5f, -5f, -5f },
                BoundsMax = new[] { 5f, 5f, 5f },
                SavePath = TestSavePath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.Greater(result.Data.VertexCount, 0);
        }
    }
}
