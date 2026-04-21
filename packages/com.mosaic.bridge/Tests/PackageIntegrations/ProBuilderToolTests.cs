#if MOSAIC_HAS_PROBUILDER
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.ProBuilder;

namespace Mosaic.Bridge.Tests.PackageIntegrations
{
    [TestFixture]
    [Category("PackageIntegration")]
    public class ProBuilderToolTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var name in new[] { "PB_Cube", "PB_Sphere", "PB_Stairs", "PB_Modify" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Create_Cube_ReturnsSuccess()
        {
            var result = ProBuilderCreateTool.Create(new ProBuilderCreateParams
            {
                Shape = "Cube", Name = "PB_Cube"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(GameObject.Find("PB_Cube"));
            Assert.IsTrue(result.Data.VertexCount > 0);
            Assert.IsTrue(result.Data.FaceCount > 0);
        }

        [Test]
        public void Create_Sphere_ReturnsSuccess()
        {
            var result = ProBuilderCreateTool.Create(new ProBuilderCreateParams
            {
                Shape = "Sphere", Name = "PB_Sphere"
            });
            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void Create_Stairs_ReturnsSuccess()
        {
            var result = ProBuilderCreateTool.Create(new ProBuilderCreateParams
            {
                Shape = "Stairs", Name = "PB_Stairs"
            });
            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void Create_InvalidShape_ReturnsFail()
        {
            var result = ProBuilderCreateTool.Create(new ProBuilderCreateParams
            {
                Shape = "InvalidShape", Name = "PB_Bad"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Info_NoMeshes_ReturnsEmptyList()
        {
            var result = ProBuilderInfoTool.Info(new ProBuilderInfoParams());
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.Meshes);
        }

        [Test]
        public void Info_AfterCreate_ReturnsMesh()
        {
            ProBuilderCreateTool.Create(new ProBuilderCreateParams { Shape = "Cube", Name = "PB_Cube" });
            var result = ProBuilderInfoTool.Info(new ProBuilderInfoParams { GameObjectName = "PB_Cube" });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Meshes.Length);
        }

        [Test]
        public void Modify_Subdivide_ReturnsSuccess()
        {
            ProBuilderCreateTool.Create(new ProBuilderCreateParams { Shape = "Cube", Name = "PB_Modify" });
            var result = ProBuilderModifyTool.Modify(new ProBuilderModifyParams
            {
                GameObjectName = "PB_Modify", Operation = "subdivide"
            });
            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void Modify_FlipNormals_ReturnsSuccess()
        {
            ProBuilderCreateTool.Create(new ProBuilderCreateParams { Shape = "Cube", Name = "PB_Modify" });
            var result = ProBuilderModifyTool.Modify(new ProBuilderModifyParams
            {
                GameObjectName = "PB_Modify", Operation = "flip-normals"
            });
            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void Modify_InvalidOperation_ReturnsFail()
        {
            ProBuilderCreateTool.Create(new ProBuilderCreateParams { Shape = "Cube", Name = "PB_Modify" });
            var result = ProBuilderModifyTool.Modify(new ProBuilderModifyParams
            {
                GameObjectName = "PB_Modify", Operation = "invalid"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Modify_NonExistentGO_ReturnsFail()
        {
            var result = ProBuilderModifyTool.Modify(new ProBuilderModifyParams
            {
                GameObjectName = "NonExistent", Operation = "subdivide"
            });
            Assert.IsFalse(result.Success);
        }
    }
}
#endif
