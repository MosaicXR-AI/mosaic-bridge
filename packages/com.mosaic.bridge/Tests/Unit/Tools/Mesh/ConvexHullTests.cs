using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.AdvancedMesh;

namespace Mosaic.Bridge.Tests.Unit.Tools.Mesh
{
    [TestFixture]
    [Category("Mesh")]
    public class ConvexHullTests
    {
        private const string TestOutputDir = "Assets/Generated/Mesh/Tests/";
        private string _fullOutputDir;
        private string _sourceMeshPath;
        private GameObject _sceneGO;

        [SetUp]
        public void SetUp()
        {
            _fullOutputDir = Path.Combine(Application.dataPath, "..", TestOutputDir);
            Directory.CreateDirectory(_fullOutputDir);
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            if (_sceneGO != null)
            {
                Object.DestroyImmediate(_sceneGO);
                _sceneGO = null;
            }

            if (Directory.Exists(_fullOutputDir))
            {
                Directory.Delete(_fullOutputDir, true);
                string metaFile = _fullOutputDir.TrimEnd('/', '\\') + ".meta";
                if (File.Exists(metaFile)) File.Delete(metaFile);
                AssetDatabase.Refresh();
            }
        }

        // ── helpers ──────────────────────────────────────────────────────

        private string CreateCubeMeshAsset(string name = "SrcCube")
        {
            var mesh = new UnityEngine.Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f,  0.5f),
            };
            mesh.triangles = new[]
            {
                0,2,1, 0,3,2,   4,5,6, 4,6,7,
                0,1,5, 0,5,4,   2,3,7, 2,7,6,
                1,2,6, 1,6,5,   0,4,7, 0,7,3
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string path = TestOutputDir + name + ".asset";
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        private string CreateSphereMeshAsset(string name = "SrcSphere")
        {
            // Dense point cloud on a sphere (ensures hull will exceed MaxVertices=32).
            var verts = new System.Collections.Generic.List<Vector3>();
            int rings = 12, sectors = 16;
            for (int r = 0; r <= rings; r++)
            {
                float v = (float)r / rings;
                float phi = Mathf.PI * v;
                for (int s = 0; s < sectors; s++)
                {
                    float u = (float)s / sectors;
                    float theta = 2f * Mathf.PI * u;
                    verts.Add(new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta)));
                }
            }

            var mesh = new UnityEngine.Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            // Trivial single-tri (we only need vertices for hull input).
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.RecalculateBounds();

            string path = TestOutputDir + name + ".asset";
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        // ── tests ────────────────────────────────────────────────────────

        [Test]
        public void Cube_ProducesHullWith8VerticesAnd12Triangles()
        {
            _sourceMeshPath = CreateCubeMeshAsset();

            var result = MeshConvexHullTool.Execute(new MeshConvexHullParams
            {
                SourceMeshPath = _sourceMeshPath,
                SavePath = TestOutputDir,
                OutputName = "Cube_Hull"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(8, result.Data.HullVertexCount, "Cube hull should have 8 vertices");
            Assert.AreEqual(12, result.Data.HullTriangleCount, "Cube hull should have 12 triangles");
            Assert.AreEqual(8, result.Data.OriginalVertexCount);
        }

        [Test]
        public void Sphere_HullVertexCountRespectsMaxVertices()
        {
            _sourceMeshPath = CreateSphereMeshAsset();
            const int max = 32;

            var result = MeshConvexHullTool.Execute(new MeshConvexHullParams
            {
                SourceMeshPath = _sourceMeshPath,
                MaxVertices = max,
                Simplify = true,
                SavePath = TestOutputDir,
                OutputName = "Sphere_Hull"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.LessOrEqual(result.Data.HullVertexCount, max,
                $"Hull vertex count {result.Data.HullVertexCount} exceeds MaxVertices {max}");
            Assert.GreaterOrEqual(result.Data.HullVertexCount, 4);
        }

        [Test]
        public void InvalidSource_ReturnsError()
        {
            var result = MeshConvexHullTool.Execute(new MeshConvexHullParams
            {
                SourceMeshPath = "Assets/DoesNotExist/nope.asset",
                SavePath = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Error);
        }

        [Test]
        public void MissingBothSources_ReturnsError()
        {
            var result = MeshConvexHullTool.Execute(new MeshConvexHullParams
            {
                SavePath = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Error);
        }

        [Test]
        public void OutputMesh_SavedAtCorrectPath()
        {
            _sourceMeshPath = CreateCubeMeshAsset("SrcForSave");

            var result = MeshConvexHullTool.Execute(new MeshConvexHullParams
            {
                SourceMeshPath = _sourceMeshPath,
                SavePath = TestOutputDir,
                OutputName = "SavedHull",
                CreateMesh = true
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.MeshPath);
            Assert.AreEqual(TestOutputDir + "SavedHull.asset", result.Data.MeshPath);

            var loaded = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(result.Data.MeshPath);
            Assert.IsNotNull(loaded, "Hull mesh asset should be loadable from disk");
            Assert.Greater(loaded.vertexCount, 0);
        }

        [Test]
        public void CreateCollider_AttachesConvexMeshCollider()
        {
            _sceneGO = new GameObject("HullTestCube");
            var mf = _sceneGO.AddComponent<MeshFilter>();

            // Build a cube mesh inline for the scene object.
            var cube = new UnityEngine.Mesh();
            cube.vertices = new[]
            {
                new Vector3(-0.5f,-0.5f,-0.5f), new Vector3( 0.5f,-0.5f,-0.5f),
                new Vector3( 0.5f, 0.5f,-0.5f), new Vector3(-0.5f, 0.5f,-0.5f),
                new Vector3(-0.5f,-0.5f, 0.5f), new Vector3( 0.5f,-0.5f, 0.5f),
                new Vector3( 0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
            };
            cube.triangles = new[]
            {
                0,2,1, 0,3,2, 4,5,6, 4,6,7,
                0,1,5, 0,5,4, 2,3,7, 2,7,6,
                1,2,6, 1,6,5, 0,4,7, 0,7,3
            };
            cube.RecalculateNormals();
            mf.sharedMesh = cube;

            var result = MeshConvexHullTool.Execute(new MeshConvexHullParams
            {
                GameObjectName = _sceneGO.name,
                CreateCollider = true,
                CreateMesh = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.ColliderAdded);

            var mc = _sceneGO.GetComponent<MeshCollider>();
            Assert.IsNotNull(mc, "MeshCollider should be attached");
            Assert.IsTrue(mc.convex, "MeshCollider.convex should be true");
            Assert.IsNotNull(mc.sharedMesh, "MeshCollider.sharedMesh should be set");
        }
    }
}
