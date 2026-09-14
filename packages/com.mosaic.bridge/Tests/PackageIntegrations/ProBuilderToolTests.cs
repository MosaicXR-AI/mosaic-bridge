#if MOSAIC_HAS_PROBUILDER
using System.Linq;
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
            foreach (var name in new[] { "PB_Cube", "PB_Sphere", "PB_Stairs", "PB_Modify", "PB_Cylinder" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }
            foreach (var go in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "Wall") Object.DestroyImmediate(go);
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

        // L2: "quad", "disc", "disk", "hemisphere", "pyramid", "wedge" used to pass the
        // known-shapes guard (they were listed as valid) but aren't real ShapeType members and
        // had no explicit switch case — they failed later at Enum.TryParse with a confusing
        // "Unknown Shape" message instead of the guard's own helpful redirect to
        // scene/create-object. They're no longer advertised as known, so they get that redirect.
        [Test]
        public void Create_RemovedNonExistentShapeName_ReturnsHelpfulGuardMessage()
        {
            var result = ProBuilderCreateTool.Create(new ProBuilderCreateParams
            {
                Shape = "Quad", Name = "PB_Bad"
            });
            Assert.IsFalse(result.Success);
            StringAssert.Contains("scene/create-object", result.Error);
        }

        // L2: GenerateCylinder's trailing smoothing argument was hardcoded to 0 (hard-shaded,
        // faceted sides) instead of using the method's own default — every cylinder this tool
        // ever created had faceted sides regardless of what the ProBuilder Editor UI itself
        // would produce for the same shape.
        [Test]
        public void Create_Cylinder_SidesAreNotHardcodedToSmoothingGroupZero()
        {
            var create = ProBuilderCreateTool.Create(new ProBuilderCreateParams
            {
                Shape = "Cylinder", Name = "PB_Cylinder"
            });
            Assert.IsTrue(create.Success, create.Error);

            var info = ProBuilderInfoTool.Info(new ProBuilderInfoParams
            {
                GameObjectName = "PB_Cylinder", Detail = "faces"
            });
            Assert.IsTrue(info.Success, info.Error);
            Assert.IsTrue(info.Data.Meshes[0].Faces.Any(f => f.SmoothingGroup != 0),
                "at least one face must carry a real smoothing group — 0 on every face means the " +
                "hardcoded value regressed");
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

        // L1: "triangulate" called ConnectElements.Connect (Subdivide, not Triangulate),
        // preceded by a dead foreach loop that did nothing. Subdividing a cube's 6 quad faces
        // would produce 24 quads (each split into 4); triangulating them produces 12 triangles —
        // the face count is what actually distinguishes the two operations.
        [Test]
        public void Modify_Triangulate_ConvertsQuadsToTriangles()
        {
            var create = ProBuilderCreateTool.Create(new ProBuilderCreateParams
            {
                Shape = "Cube", Name = "PB_Modify"
            });
            Assert.IsTrue(create.Success, create.Error);

            var result = ProBuilderModifyTool.Modify(new ProBuilderModifyParams
            {
                GameObjectName = "PB_Modify", Operation = "triangulate"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(12, result.Data.FaceCount,
                "a cube's 6 quads must become 12 triangles — 24 would mean Subdivide ran instead");
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

        // O4 L18: "parts lists reuse names like 'Wall'" — GameObject.Find silently returned the
        // first match with no warning. Two objects sharing a name must now fail loudly by bare
        // name, and resolve unambiguously via InstanceId.
        [Test]
        public void Modify_DuplicateName_FailsWithAmbiguousError()
        {
            var first = ProBuilderCreateTool.Create(new ProBuilderCreateParams { Shape = "Cube", Name = "Wall" });
            var second = ProBuilderCreateTool.Create(new ProBuilderCreateParams { Shape = "Cube", Name = "Wall" });
            Assert.IsTrue(first.Success, first.Error);
            Assert.IsTrue(second.Success, second.Error);

            var result = ProBuilderModifyTool.Modify(new ProBuilderModifyParams
            {
                GameObjectName = "Wall", Operation = "subdivide"
            });
            Assert.IsFalse(result.Success);
            StringAssert.Contains("matches 2 GameObjects", result.Error);
        }

        [Test]
        public void Modify_DuplicateName_ResolvesByInstanceId()
        {
            var first = ProBuilderCreateTool.Create(new ProBuilderCreateParams { Shape = "Cube", Name = "Wall" });
            ProBuilderCreateTool.Create(new ProBuilderCreateParams { Shape = "Cube", Name = "Wall" });

            var result = ProBuilderModifyTool.Modify(new ProBuilderModifyParams
            {
                InstanceId = first.Data.InstanceId, Operation = "subdivide"
            });
            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void Info_DuplicateName_ResolvesByInstanceId()
        {
            ProBuilderCreateTool.Create(new ProBuilderCreateParams { Shape = "Cube", Name = "Wall" });
            var second = ProBuilderCreateTool.Create(new ProBuilderCreateParams { Shape = "Sphere", Name = "Wall" });

            var result = ProBuilderInfoTool.Info(new ProBuilderInfoParams { InstanceId = second.Data.InstanceId });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Meshes.Length);
            Assert.AreEqual(second.Data.InstanceId, result.Data.Meshes[0].InstanceId);
        }
    }
}
#endif
