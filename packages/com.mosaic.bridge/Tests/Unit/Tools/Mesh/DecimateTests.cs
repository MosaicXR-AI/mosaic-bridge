using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.AdvancedMesh;

namespace Mosaic.Bridge.Tests.Unit.Tools.Mesh
{
    [TestFixture]
    [Category("Unit")]
    public class DecimateTests
    {
        const string TempDir = "Assets/Generated/MeshDecimateTests/";

        GameObject _go;
        UnityEngine.Mesh _sourceMesh;
        string _sourceAssetPath;

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", TempDir)));

            // Build a plane 10x10 → 121 verts, 200 triangles. Enough to decimate.
            _sourceMesh = BuildPlane(10);
            _sourceAssetPath = TempDir + "decimate_src.asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(_sourceAssetPath) != null)
                AssetDatabase.DeleteAsset(_sourceAssetPath);
            AssetDatabase.CreateAsset(_sourceMesh, _sourceAssetPath);
            AssetDatabase.SaveAssets();

            _go = new GameObject("DecimateTestGO");
            var mf = _go.AddComponent<MeshFilter>();
            mf.sharedMesh = _sourceMesh;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            // Clean up generated assets — fail-safe, don't throw on missing dirs
            if (AssetDatabase.IsValidFolder(TempDir.TrimEnd('/')))
                AssetDatabase.DeleteAsset(TempDir.TrimEnd('/'));
            if (AssetDatabase.IsValidFolder("Assets/Generated/Mesh"))
                AssetDatabase.DeleteAsset("Assets/Generated/Mesh");
        }

        // -----------------------------------------------------------------
        // QualityRatio=1.0 returns same triangle count
        // -----------------------------------------------------------------
        [Test]
        public void QualityRatio_One_ReturnsSameMesh()
        {
            var result = MeshDecimateTool.Execute(new MeshDecimateParams
            {
                SourceMeshPath = _sourceAssetPath,
                QualityRatio = 1f,
                SavePath = TempDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.MeshPaths.Length);
            Assert.AreEqual(result.Data.OriginalTriangleCount,
                            result.Data.DecimatedTriangleCounts[0]);
            Assert.AreEqual(result.Data.OriginalVertexCount,
                            result.Data.DecimatedVertexCounts[0]);
        }

        // -----------------------------------------------------------------
        // QualityRatio=0.5 reduces triangle count
        // -----------------------------------------------------------------
        [Test]
        public void QualityRatio_Half_ReducesTriangleCount()
        {
            var result = MeshDecimateTool.Execute(new MeshDecimateParams
            {
                SourceMeshPath = _sourceAssetPath,
                QualityRatio = 0.5f,
                PreserveBoundary = false,
                PreserveUVSeams = false,
                SavePath = TempDir
            });

            Assert.IsTrue(result.Success, result.Error);
            int orig = result.Data.OriginalTriangleCount;
            int decim = result.Data.DecimatedTriangleCounts[0];
            Assert.Less(decim, orig, "Decimated triangle count should be less than original");
            // MVP clustering is approximate — allow a wide tolerance but ensure meaningful reduction.
            Assert.Less(decim, (int)(orig * 0.9f),
                $"Expected meaningful reduction, got {decim}/{orig}");
        }

        // -----------------------------------------------------------------
        // GenerateLODGroup creates a GameObject with a LODGroup component
        // -----------------------------------------------------------------
        [Test]
        public void GenerateLODGroup_CreatesLodGroupGameObject()
        {
            var result = MeshDecimateTool.Execute(new MeshDecimateParams
            {
                SourceMeshPath = _sourceAssetPath,
                GenerateLODGroup = true,
                LodLevels = new[] { 1f, 0.5f, 0.25f },
                SavePath = TempDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.LodGroupGameObject);

            var go = GameObject.Find(result.Data.LodGroupGameObject);
            Assert.IsNotNull(go, "LODGroup GameObject not found in scene");
            var lodGroup = go.GetComponent<LODGroup>();
            Assert.IsNotNull(lodGroup, "LODGroup component missing");
            Assert.AreEqual(3, lodGroup.lodCount);

            Object.DestroyImmediate(go);
        }

        // -----------------------------------------------------------------
        // LodLevels array produces correct number of meshes
        // -----------------------------------------------------------------
        [Test]
        public void LodLevels_ArrayProducesCorrectMeshCount()
        {
            var ratios = new[] { 1f, 0.6f, 0.3f, 0.1f };
            var result = MeshDecimateTool.Execute(new MeshDecimateParams
            {
                SourceMeshPath = _sourceAssetPath,
                LodLevels = ratios,
                SavePath = TempDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(ratios.Length, result.Data.MeshPaths.Length);
            Assert.AreEqual(ratios.Length, result.Data.DecimatedTriangleCounts.Length);
            Assert.AreEqual(ratios.Length, result.Data.DecimatedVertexCounts.Length);
        }

        // -----------------------------------------------------------------
        // Invalid source returns error
        // -----------------------------------------------------------------
        [Test]
        public void InvalidSourceMeshPath_ReturnsError()
        {
            var result = MeshDecimateTool.Execute(new MeshDecimateParams
            {
                SourceMeshPath = "Assets/NonExistent/FakeMesh.asset",
                QualityRatio = 0.5f,
                SavePath = TempDir
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void MissingSourceAndGameObject_ReturnsError()
        {
            var result = MeshDecimateTool.Execute(new MeshDecimateParams
            {
                QualityRatio = 0.5f,
                SavePath = TempDir
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void GameObjectName_WithoutMeshFilter_ReturnsError()
        {
            var empty = new GameObject("EmptyDecimateGO");
            try
            {
                var result = MeshDecimateTool.Execute(new MeshDecimateParams
                {
                    GameObjectName = "EmptyDecimateGO",
                    QualityRatio = 0.5f,
                    SavePath = TempDir
                });
                Assert.IsFalse(result.Success);
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        // -----------------------------------------------------------------
        // Boundary preservation keeps the four corner vertices of the plane
        // -----------------------------------------------------------------
        [Test]
        public void PreserveBoundary_KeepsCornerVertices()
        {
            var result = MeshDecimateTool.Execute(new MeshDecimateParams
            {
                SourceMeshPath = _sourceAssetPath,
                QualityRatio = 0.2f,
                PreserveBoundary = true,
                PreserveUVSeams = false,
                SavePath = TempDir
            });

            Assert.IsTrue(result.Success, result.Error);

            var decimated = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(result.Data.MeshPaths[0]);
            Assert.IsNotNull(decimated);
            var verts = decimated.vertices;

            // Plane spans (0,0,0) to (1,0,1). All four corners must be present.
            Vector3[] corners =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(1f, 0f, 1f)
            };
            foreach (var corner in corners)
            {
                bool found = false;
                for (int i = 0; i < verts.Length; i++)
                {
                    if (Vector3.SqrMagnitude(verts[i] - corner) < 1e-4f)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found, $"Corner {corner} missing after boundary-preserving decimation");
            }
        }

        // -----------------------------------------------------------------
        // GameObjectName source path works
        // -----------------------------------------------------------------
        [Test]
        public void GameObjectName_SourceWorks()
        {
            var result = MeshDecimateTool.Execute(new MeshDecimateParams
            {
                GameObjectName = "DecimateTestGO",
                QualityRatio = 0.5f,
                SavePath = TempDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.MeshPaths.Length);
            Assert.Greater(result.Data.OriginalTriangleCount, 0);
        }

        // -----------------------------------------------------------------
        // Helper: build a flat plane mesh, subdivided N×N.
        // -----------------------------------------------------------------
        static UnityEngine.Mesh BuildPlane(int segments)
        {
            int verts = (segments + 1) * (segments + 1);
            var v = new Vector3[verts];
            var uv = new Vector2[verts];
            var n = new Vector3[verts];
            var tris = new int[segments * segments * 6];

            for (int z = 0; z <= segments; z++)
            {
                for (int x = 0; x <= segments; x++)
                {
                    int idx = z * (segments + 1) + x;
                    float fx = (float)x / segments;
                    float fz = (float)z / segments;
                    v[idx] = new Vector3(fx, 0f, fz);
                    uv[idx] = new Vector2(fx, fz);
                    n[idx] = Vector3.up;
                }
            }
            int ti = 0;
            for (int z = 0; z < segments; z++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int bl = z * (segments + 1) + x;
                    int br = bl + 1;
                    int tl = bl + (segments + 1);
                    int tr = tl + 1;
                    tris[ti++] = bl; tris[ti++] = tl; tris[ti++] = br;
                    tris[ti++] = br; tris[ti++] = tl; tris[ti++] = tr;
                }
            }

            var m = new UnityEngine.Mesh { name = "DecimateSource" };
            m.vertices = v;
            m.uv = uv;
            m.normals = n;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }
    }
}
