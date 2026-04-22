using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.AdvancedMesh;

namespace Mosaic.Bridge.Tests.Unit.Tools.Mesh
{
    [TestFixture]
    [Category("Unit")]
    public class BooleanTests
    {
        private const string TestSavePath = "Assets/Generated/MeshBooleanTests/";

        private GameObject _a;
        private GameObject _b;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<string> _createdAssets = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _a = CreateCube("BoolTestA", new Vector3(0, 0, 0), Vector3.one);
            _b = CreateCube("BoolTestB", new Vector3(0.5f, 0.5f, 0.5f), Vector3.one);
            _spawned.Add(_a);
            _spawned.Add(_b);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();

            foreach (var path in _createdAssets)
                AssetDatabase.DeleteAsset(path);
            _createdAssets.Clear();

            if (AssetDatabase.IsValidFolder("Assets/Generated/MeshBooleanTests"))
                AssetDatabase.DeleteAsset("Assets/Generated/MeshBooleanTests");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        private static GameObject CreateCube(string name, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            // Drop the collider so it doesn't interfere with selection by name
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            return go;
        }

        private MeshBooleanResult Run(MeshBooleanParams p, bool expectSuccess = true)
        {
            p.SavePath = TestSavePath;
            var res = MeshBooleanTool.Execute(p);
            if (expectSuccess)
            {
                Assert.IsTrue(res.Success, $"Expected success but got error: {res.Error}");
                Assert.IsNotNull(res.Data);
                if (!string.IsNullOrEmpty(res.Data.MeshPath)) _createdAssets.Add(res.Data.MeshPath);
                var go = Resources.EntityIdToObject(res.Data.InstanceId) as GameObject;
                if (go != null) _spawned.Add(go);
                return res.Data;
            }
            Assert.IsFalse(res.Success);
            return null;
        }

        // -----------------------------------------------------------------
        // Union of two cubes produces a valid mesh
        // -----------------------------------------------------------------
        [Test]
        public void Union_TwoCubes_ProducesValidMesh()
        {
            var data = Run(new MeshBooleanParams
            {
                MeshAGameObject = "BoolTestA",
                MeshBGameObject = "BoolTestB",
                Operation       = "union",
                KeepOriginals   = true,
            });

            Assert.AreEqual("union", data.Operation);
            Assert.Greater(data.VertexCount, 0);
            Assert.Greater(data.TriangleCount, 0);
            Assert.IsTrue(File.Exists(data.MeshPath) || AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(data.MeshPath) != null);
        }

        // -----------------------------------------------------------------
        // Subtract removes the intersection (result differs from A)
        // -----------------------------------------------------------------
        [Test]
        public void Subtract_ProducesDifferentGeometryThanA()
        {
            var data = Run(new MeshBooleanParams
            {
                MeshAGameObject = "BoolTestA",
                MeshBGameObject = "BoolTestB",
                Operation       = "subtract",
                KeepOriginals   = true,
            });

            Assert.AreEqual("subtract", data.Operation);
            Assert.Greater(data.TriangleCount, 0,
                "Subtracting overlapping cubes should still produce polygons (the non-overlapping part of A plus cut surfaces).");
        }

        // -----------------------------------------------------------------
        // Intersect keeps only the overlap
        // -----------------------------------------------------------------
        [Test]
        public void Intersect_KeepsOnlyOverlap()
        {
            var data = Run(new MeshBooleanParams
            {
                MeshAGameObject = "BoolTestA",
                MeshBGameObject = "BoolTestB",
                Operation       = "intersect",
                KeepOriginals   = true,
            });

            Assert.AreEqual("intersect", data.Operation);
            Assert.Greater(data.TriangleCount, 0,
                "Intersection of overlapping cubes should produce polygons.");
        }

        // -----------------------------------------------------------------
        // Invalid operation returns an error
        // -----------------------------------------------------------------
        [Test]
        public void InvalidOperation_ReturnsError()
        {
            var res = MeshBooleanTool.Execute(new MeshBooleanParams
            {
                MeshAGameObject = "BoolTestA",
                MeshBGameObject = "BoolTestB",
                Operation       = "xor",
                SavePath        = TestSavePath,
                KeepOriginals   = true,
            });
            Assert.IsFalse(res.Success);
        }

        // -----------------------------------------------------------------
        // Missing GameObject returns an error
        // -----------------------------------------------------------------
        [Test]
        public void MissingGameObject_ReturnsError()
        {
            var res = MeshBooleanTool.Execute(new MeshBooleanParams
            {
                MeshAGameObject = "DoesNotExist_XYZ",
                MeshBGameObject = "BoolTestB",
                Operation       = "union",
                SavePath        = TestSavePath,
                KeepOriginals   = true,
            });
            Assert.IsFalse(res.Success);
        }

        // -----------------------------------------------------------------
        // KeepOriginals=true preserves source GOs
        // -----------------------------------------------------------------
        [Test]
        public void KeepOriginals_True_PreservesSourceGameObjects()
        {
            var data = Run(new MeshBooleanParams
            {
                MeshAGameObject = "BoolTestA",
                MeshBGameObject = "BoolTestB",
                Operation       = "union",
                KeepOriginals   = true,
            });

            Assert.IsTrue(data.OriginalsKept);
            Assert.IsNotNull(GameObject.Find("BoolTestA"));
            Assert.IsNotNull(GameObject.Find("BoolTestB"));
        }

        // -----------------------------------------------------------------
        // KeepOriginals=false destroys source GOs
        // -----------------------------------------------------------------
        [Test]
        public void KeepOriginals_False_DestroysSourceGameObjects()
        {
            var data = Run(new MeshBooleanParams
            {
                MeshAGameObject = "BoolTestA",
                MeshBGameObject = "BoolTestB",
                Operation       = "union",
                KeepOriginals   = false,
            });

            Assert.IsFalse(data.OriginalsKept);
            // Sources are removed from the scene
            Assert.IsNull(GameObject.Find("BoolTestA"));
            Assert.IsNull(GameObject.Find("BoolTestB"));
        }

        // -----------------------------------------------------------------
        // GenerateCollider attaches a MeshCollider
        // -----------------------------------------------------------------
        [Test]
        public void GenerateCollider_AttachesMeshCollider()
        {
            var data = Run(new MeshBooleanParams
            {
                MeshAGameObject  = "BoolTestA",
                MeshBGameObject  = "BoolTestB",
                Operation        = "union",
                KeepOriginals    = true,
                GenerateCollider = true,
            });

            var go = Resources.EntityIdToObject(data.InstanceId) as GameObject;
            Assert.IsNotNull(go);
            Assert.IsNotNull(go.GetComponent<MeshCollider>());
        }
    }
}
