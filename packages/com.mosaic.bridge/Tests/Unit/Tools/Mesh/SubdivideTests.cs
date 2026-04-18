using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.AdvancedMesh;

namespace Mosaic.Bridge.Tests.Unit.Tools.Mesh
{
    [TestFixture]
    [Category("AdvancedMesh")]
    public class SubdivideTests
    {
        private const string TestOutputDir = "Assets/Generated/Mesh/SubdivideTests/";
        private string _fullOutputDir;
        private string _sourceMeshPath;

        [SetUp]
        public void SetUp()
        {
            _fullOutputDir = Path.Combine(Application.dataPath, "..", TestOutputDir);
            Directory.CreateDirectory(_fullOutputDir);

            // Persist a simple cube mesh asset as source.
            var cube = BuildCube();
            _sourceMeshPath = TestOutputDir + "SrcCube.asset";
            AssetDatabase.CreateAsset(cube, _sourceMeshPath);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_fullOutputDir))
            {
                Directory.Delete(_fullOutputDir, true);
                string metaFile = _fullOutputDir.TrimEnd('/', '\\') + ".meta";
                if (File.Exists(metaFile)) File.Delete(metaFile);
                AssetDatabase.Refresh();
            }
        }

        // ── 1 iteration on a cube roughly quadruples triangle count ───────

        [Test]
        public void LoopSubdivide_OneIteration_QuadruplesTriangleCount()
        {
            var result = MeshSubdivideTool.Execute(new MeshSubdivideParams
            {
                SourceMeshPath = _sourceMeshPath,
                Method = "loop",
                Iterations = 1,
                PreserveCreases = false,
                SavePath = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(12, result.Data.OriginalTriangleCount, "Cube has 12 triangles");
            Assert.AreEqual(48, result.Data.NewTriangleCount, "1 Loop iteration should yield 4× triangles");
        }

        // ── 2 iterations produce 16x triangles ────────────────────────────

        [Test]
        public void LoopSubdivide_TwoIterations_SixteenTimesTriangles()
        {
            var result = MeshSubdivideTool.Execute(new MeshSubdivideParams
            {
                SourceMeshPath = _sourceMeshPath,
                Method = "loop",
                Iterations = 2,
                PreserveCreases = false,
                SavePath = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(12 * 16, result.Data.NewTriangleCount,
                "2 Loop iterations should yield 16× triangles");
        }

        // ── Invalid method returns error ──────────────────────────────────

        [Test]
        public void InvalidMethod_ReturnsError()
        {
            var result = MeshSubdivideTool.Execute(new MeshSubdivideParams
            {
                SourceMeshPath = _sourceMeshPath,
                Method = "not_a_method",
                Iterations = 1,
                SavePath = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Iterations clamped to [1, 4] ──────────────────────────────────

        [Test]
        public void Iterations_ClampedToMin()
        {
            var result = MeshSubdivideTool.Execute(new MeshSubdivideParams
            {
                SourceMeshPath = _sourceMeshPath,
                Method = "loop",
                Iterations = 0,
                SavePath = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Iterations);
        }

        [Test]
        public void Iterations_ClampedToMax()
        {
            var result = MeshSubdivideTool.Execute(new MeshSubdivideParams
            {
                SourceMeshPath = _sourceMeshPath,
                Method = "loop",
                Iterations = 99,
                SavePath = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(4, result.Data.Iterations);
        }

        // ── Output mesh has valid normals (no zero-length) ────────────────

        [Test]
        public void OutputMesh_HasValidNormals()
        {
            var result = MeshSubdivideTool.Execute(new MeshSubdivideParams
            {
                SourceMeshPath = _sourceMeshPath,
                Method = "loop",
                Iterations = 1,
                PreserveCreases = true,
                SavePath = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);

            var mesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(result.Data.MeshPath);
            Assert.IsNotNull(mesh, "Saved mesh asset should load");
            Assert.Greater(mesh.normals.Length, 0, "Normals should be generated");

            foreach (var n in mesh.normals)
                Assert.Greater(n.sqrMagnitude, 0.0001f, "Normal should not be zero-length");
        }

        // ── Boundary vertices preserved when PreserveCreases=true ─────────
        // A flat open quad (2 triangles) has all 4 vertices on the boundary;
        // PreserveCreases should leave them untouched after subdivision.

        [Test]
        public void PreserveCreases_KeepsBoundaryVertices()
        {
            // Build an open flat quad mesh (2 tris, all vertices on boundary).
            var quad = new UnityEngine.Mesh { name = "FlatQuad" };
            quad.SetVertices(new List<Vector3>
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 1),
                new Vector3(0, 0, 1),
            });
            quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            quad.RecalculateNormals();
            quad.RecalculateBounds();

            string quadPath = TestOutputDir + "FlatQuad.asset";
            AssetDatabase.CreateAsset(quad, quadPath);
            AssetDatabase.SaveAssets();

            var original = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 1),
                new Vector3(0, 0, 1),
            };

            var result = MeshSubdivideTool.Execute(new MeshSubdivideParams
            {
                SourceMeshPath = quadPath,
                Method = "loop",
                Iterations = 1,
                PreserveCreases = true,
                SavePath = TestOutputDir,
                OutputName = "FlatQuad_Preserved"
            });

            Assert.IsTrue(result.Success, result.Error);

            var subd = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(result.Data.MeshPath);
            var verts = subd.vertices;

            // First 4 vertices correspond to original corners; all lie on boundary.
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(original[i].x, verts[i].x, 0.0001f, $"V{i}.x drifted from boundary");
                Assert.AreEqual(original[i].y, verts[i].y, 0.0001f, $"V{i}.y drifted from boundary");
                Assert.AreEqual(original[i].z, verts[i].z, 0.0001f, $"V{i}.z drifted from boundary");
            }
        }

        // ── Catmull-Clark accepted as method alias ────────────────────────

        [Test]
        public void CatmullClark_Method_Succeeds()
        {
            var result = MeshSubdivideTool.Execute(new MeshSubdivideParams
            {
                SourceMeshPath = _sourceMeshPath,
                Method = "catmull_clark",
                Iterations = 1,
                SavePath = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("catmull_clark", result.Data.Method);
        }

        // ── sqrt3 method: triangle count triples per iteration ────────────

        [Test]
        public void Sqrt3_OneIteration_TriplesTriangleCount()
        {
            var result = MeshSubdivideTool.Execute(new MeshSubdivideParams
            {
                SourceMeshPath = _sourceMeshPath,
                Method = "sqrt3",
                Iterations = 1,
                SavePath = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(36, result.Data.NewTriangleCount,
                "sqrt3 barycentric subdivision triples triangles (12 → 36)");
        }

        // ── Missing source returns error ──────────────────────────────────

        [Test]
        public void NoSource_ReturnsError()
        {
            var result = MeshSubdivideTool.Execute(new MeshSubdivideParams
            {
                Method = "loop",
                Iterations = 1,
                SavePath = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static UnityEngine.Mesh BuildCube()
        {
            var mesh = new UnityEngine.Mesh { name = "TestCube" };
            var verts = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), // 0
                new Vector3( 0.5f, -0.5f, -0.5f), // 1
                new Vector3( 0.5f,  0.5f, -0.5f), // 2
                new Vector3(-0.5f,  0.5f, -0.5f), // 3
                new Vector3(-0.5f, -0.5f,  0.5f), // 4
                new Vector3( 0.5f, -0.5f,  0.5f), // 5
                new Vector3( 0.5f,  0.5f,  0.5f), // 6
                new Vector3(-0.5f,  0.5f,  0.5f), // 7
            };
            var tris = new int[]
            {
                // back
                0, 2, 1, 0, 3, 2,
                // front
                4, 5, 6, 4, 6, 7,
                // left
                0, 4, 7, 0, 7, 3,
                // right
                1, 2, 6, 1, 6, 5,
                // bottom
                0, 1, 5, 0, 5, 4,
                // top
                3, 7, 6, 3, 6, 2,
            };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
