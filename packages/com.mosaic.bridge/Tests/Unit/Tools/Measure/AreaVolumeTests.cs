using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Measure;

namespace Mosaic.Bridge.Tests.Unit.Tools.Measure
{
    [TestFixture]
    [Category("Unit")]
    public class AreaVolumeTests
    {
        private GameObject _sceneGO;

        [TearDown]
        public void TearDown()
        {
            if (_sceneGO != null)
            {
                Object.DestroyImmediate(_sceneGO);
                _sceneGO = null;
            }
            // Clean up any visual objects left behind.
            var visual = GameObject.Find("MeasureArea_Visual");
            if (visual != null) Object.DestroyImmediate(visual);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static UnityEngine.Mesh MakeCubeMesh()
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
            // Consistent outward winding; each edge shared by exactly two triangles
            // with opposite directions (watertight).
            mesh.triangles = new[]
            {
                0,2,1, 0,3,2,   // -z
                4,5,6, 4,6,7,   // +z
                0,1,5, 0,5,4,   // -y
                2,3,7, 2,7,6,   // +y
                1,2,6, 1,6,5,   // +x
                0,4,7, 0,7,3    // -x
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private GameObject CreateCubeGO(string name = "TestCube")
        {
            _sceneGO = new GameObject(name);
            var mf = _sceneGO.AddComponent<MeshFilter>();
            mf.sharedMesh = MakeCubeMesh();
            return _sceneGO;
        }

        // ── Polygon area ─────────────────────────────────────────────────────

        [Test]
        public void SquarePolygon_Area_Is1SquareMeter()
        {
            var res = MeasureAreaTool.Execute(new MeasureAreaParams
            {
                Polygon = new[]
                {
                    new[] { 0f, 0f, 0f },
                    new[] { 1f, 0f, 0f },
                    new[] { 1f, 0f, 1f },
                    new[] { 0f, 0f, 1f }
                },
                Unit = "m2"
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual(1f, res.Data.Area, 0.0001f);
            Assert.AreEqual("m2", res.Data.Unit);
            Assert.AreEqual(4, res.Data.VertexCount);
            Assert.AreEqual(2, res.Data.TriangleCount);
        }

        [Test]
        public void SquarePolygon_UnitConversion_ToFt2()
        {
            var res = MeasureAreaTool.Execute(new MeasureAreaParams
            {
                Polygon = new[]
                {
                    new[] { 0f, 0f, 0f },
                    new[] { 1f, 0f, 0f },
                    new[] { 1f, 0f, 1f },
                    new[] { 0f, 0f, 1f }
                },
                Unit = "ft2"
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual(10.7639f, res.Data.Area, 0.001f);
        }

        // ── Mesh area ────────────────────────────────────────────────────────

        [Test]
        public void CubeSurfaceArea_Is6SquareMeters()
        {
            var go = CreateCubeGO();

            var res = MeasureAreaTool.Execute(new MeasureAreaParams
            {
                GameObjectName = go.name,
                Unit = "m2"
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual(6f, res.Data.Area, 0.0001f);
            Assert.AreEqual(12, res.Data.TriangleCount);
        }

        // ── Volume ───────────────────────────────────────────────────────────

        [Test]
        public void CubeVolume_Is1CubicMeter()
        {
            var go = CreateCubeGO();

            var res = MeasureVolumeTool.Execute(new MeasureVolumeParams
            {
                GameObjectName = go.name,
                Unit = "m3"
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual(1f, res.Data.Volume, 0.0001f);
            Assert.IsTrue(res.Data.IsClosed, "Cube should be watertight");
            Assert.AreEqual(6f, res.Data.SurfaceArea, 0.0001f);
        }

        [Test]
        public void CubeVolume_UnitConversion_ToLiters()
        {
            var go = CreateCubeGO();

            var res = MeasureVolumeTool.Execute(new MeasureVolumeParams
            {
                GameObjectName = go.name,
                Unit = "liters"
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.AreEqual(1000f, res.Data.Volume, 0.01f);
        }

        [Test]
        public void SphereVolume_ApproximatesFourThirdsPiRCubed()
        {
            // Build a UV sphere mesh (radius = 1) with consistent winding.
            const float r = 1f;
            const int stacks = 24;
            const int slices = 48;

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris  = new System.Collections.Generic.List<int>();

            for (int i = 0; i <= stacks; i++)
            {
                float v = i / (float)stacks;
                float phi = v * Mathf.PI;
                for (int j = 0; j <= slices; j++)
                {
                    float u = j / (float)slices;
                    float theta = u * Mathf.PI * 2f;
                    float x = r * Mathf.Sin(phi) * Mathf.Cos(theta);
                    float y = r * Mathf.Cos(phi);
                    float z = r * Mathf.Sin(phi) * Mathf.Sin(theta);
                    verts.Add(new Vector3(x, y, z));
                }
            }

            int ring = slices + 1;
            for (int i = 0; i < stacks; i++)
            {
                for (int j = 0; j < slices; j++)
                {
                    int a = i * ring + j;
                    int b = a + 1;
                    int c = a + ring;
                    int d = c + 1;
                    // Outward-facing winding
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            var mesh = new UnityEngine.Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _sceneGO = new GameObject("TestSphere");
            var mf = _sceneGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var res = MeasureVolumeTool.Execute(new MeasureVolumeParams
            {
                GameObjectName = _sceneGO.name,
                Unit = "m3"
            });

            Assert.IsTrue(res.Success, res.Error);
            float expected = (4f / 3f) * Mathf.PI * r * r * r;
            // Tessellated sphere underestimates by a couple percent; allow 3% tolerance.
            Assert.AreEqual(expected, res.Data.Volume, expected * 0.03f);
        }

        [Test]
        public void NonClosedMesh_ReportsIsClosedFalse()
        {
            // Single triangle: obviously not closed.
            var mesh = new UnityEngine.Mesh();
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 0f, 1f),
            };
            // 3 open triangles (tetra missing one face) => some edges unshared
            mesh.triangles = new[] { 0, 1, 2,  0, 2, 3,  0, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            _sceneGO = new GameObject("OpenMesh");
            var mf = _sceneGO.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var res = MeasureVolumeTool.Execute(new MeasureVolumeParams
            {
                GameObjectName = _sceneGO.name,
                Unit = "m3"
            });

            Assert.IsTrue(res.Success, res.Error);
            Assert.IsFalse(res.Data.IsClosed, "Open mesh should not be watertight");
        }

        // ── Invalid input ────────────────────────────────────────────────────

        [Test]
        public void Area_NullParams_Error()
        {
            var res = MeasureAreaTool.Execute(null);
            Assert.IsFalse(res.Success);
        }

        [Test]
        public void Area_NoInputs_Error()
        {
            var res = MeasureAreaTool.Execute(new MeasureAreaParams());
            Assert.IsFalse(res.Success);
        }

        [Test]
        public void Area_TooFewPolygonVertices_Error()
        {
            var res = MeasureAreaTool.Execute(new MeasureAreaParams
            {
                Polygon = new[] { new[] { 0f, 0f, 0f }, new[] { 1f, 0f, 0f } }
            });
            Assert.IsFalse(res.Success);
        }

        [Test]
        public void Area_InvalidUnit_Error()
        {
            var res = MeasureAreaTool.Execute(new MeasureAreaParams
            {
                Polygon = new[]
                {
                    new[] { 0f, 0f, 0f },
                    new[] { 1f, 0f, 0f },
                    new[] { 1f, 0f, 1f }
                },
                Unit = "acres"
            });
            Assert.IsFalse(res.Success);
        }

        [Test]
        public void Volume_MissingGameObject_Error()
        {
            var res = MeasureVolumeTool.Execute(new MeasureVolumeParams());
            Assert.IsFalse(res.Success);
        }

        [Test]
        public void Volume_NonexistentGameObject_Error()
        {
            var res = MeasureVolumeTool.Execute(new MeasureVolumeParams
            {
                GameObjectName = "DoesNotExist__" + System.Guid.NewGuid()
            });
            Assert.IsFalse(res.Success);
        }

        [Test]
        public void Volume_InvalidUnit_Error()
        {
            var go = CreateCubeGO();
            var res = MeasureVolumeTool.Execute(new MeasureVolumeParams
            {
                GameObjectName = go.name,
                Unit = "gallons"
            });
            Assert.IsFalse(res.Success);
        }
    }
}
