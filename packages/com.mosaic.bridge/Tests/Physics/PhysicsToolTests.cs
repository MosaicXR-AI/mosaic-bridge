using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.Tests.Physics
{
    /// <summary>
    /// Edit-mode tests for Physics tools.
    /// Creates a GameObject with a collider, raycasts against it, verifies the hit,
    /// then cleans up.
    /// </summary>
    [TestFixture]
    [Category("Physics")]
    public class PhysicsToolTests
    {
        private GameObject _testGo;

        [SetUp]
        public void SetUp()
        {
            _testGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _testGo.name = "PhysicsTestCube";
            _testGo.transform.position = Vector3.zero;
            // Ensure the physics world is aware of the collider
            UnityEngine.Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null)
                Object.DestroyImmediate(_testGo);
        }

        // ── Raycast ─────────────────────────────────────────────────────────

        [Test]
        public void Raycast_HittingCube_ReturnsHit()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsRaycastParams
            {
                Origin    = new float[] { 0f, 10f, 0f },
                Direction = new float[] { 0f, -1f, 0f },
                MaxDistance = 100f
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsRaycastTool.Execute(p);

            Assert.IsTrue(result.Success, $"Raycast should succeed. Error: {result.Error}");
            Assert.IsTrue(result.Data.Hit, "Raycast should hit the cube");
            Assert.AreEqual("PhysicsTestCube", result.Data.GameObjectName);
            Assert.IsNotNull(result.Data.Point);
            Assert.IsNotNull(result.Data.Normal);
            Assert.Greater(result.Data.Distance, 0f);
        }

        [Test]
        public void Raycast_MissingCube_ReturnsNoHit()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsRaycastParams
            {
                Origin    = new float[] { 100f, 10f, 0f },
                Direction = new float[] { 0f, -1f, 0f },
                MaxDistance = 5f
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsRaycastTool.Execute(p);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Data.Hit, "Raycast should miss the cube");
        }

        [Test]
        public void Raycast_InvalidOrigin_ReturnsError()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsRaycastParams
            {
                Origin    = new float[] { 0f, 10f },  // only 2 elements
                Direction = new float[] { 0f, -1f, 0f }
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsRaycastTool.Execute(p);

            Assert.IsFalse(result.Success);
        }

        // ── Add Rigidbody ───────────────────────────────────────────────────

        [Test]
        public void AddRigidbody_DefaultParams_SetsDefaults()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsAddRigidbodyParams
            {
                Name = "PhysicsTestCube"
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsAddRigidbodyTool.Execute(p);

            Assert.IsTrue(result.Success, $"Should succeed. Error: {result.Error}");
            Assert.AreEqual("PhysicsTestCube", result.Data.GameObjectName);
            Assert.AreEqual(1f, result.Data.Mass);
            Assert.IsTrue(result.Data.UseGravity);
            Assert.IsFalse(result.Data.IsKinematic);

            // Verify component actually exists
            Assert.IsNotNull(_testGo.GetComponent<Rigidbody>());
        }

        [Test]
        public void AddRigidbody_CustomParams_AppliesValues()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsAddRigidbodyParams
            {
                Name        = "PhysicsTestCube",
                Mass        = 5f,
                UseGravity  = false,
                IsKinematic = true
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsAddRigidbodyTool.Execute(p);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(5f, result.Data.Mass);
            Assert.IsFalse(result.Data.UseGravity);
            Assert.IsTrue(result.Data.IsKinematic);
        }

        [Test]
        public void AddRigidbody_NonexistentGO_ReturnsError()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsAddRigidbodyParams
            {
                Name = "NonexistentObject_12345"
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsAddRigidbodyTool.Execute(p);

            Assert.IsFalse(result.Success);
        }

        // L9: Undo.AddComponent<Rigidbody> returns null when one already exists, and every field
        // access on that null used to throw NullReferenceException instead of a clean error.
        [Test]
        public void AddRigidbody_AlreadyPresent_ReturnsCleanErrorNotException()
        {
            Undo.AddComponent<Rigidbody>(_testGo);

            var result = Mosaic.Bridge.Tools.Physics.PhysicsAddRigidbodyTool.Execute(
                new Mosaic.Bridge.Tools.Physics.PhysicsAddRigidbodyParams { Name = "PhysicsTestCube" });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("already has a Rigidbody", result.Error);
        }

        // ── Add Collider ────────────────────────────────────────────────────

        [Test]
        public void AddCollider_BoxType_AddsBoxCollider()
        {
            // Remove existing collider first
            var existing = _testGo.GetComponent<Collider>();
            if (existing != null) Object.DestroyImmediate(existing);

            var p = new Mosaic.Bridge.Tools.Physics.PhysicsAddColliderParams
            {
                Name = "PhysicsTestCube",
                Type = "Box"
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsAddColliderTool.Execute(p);

            Assert.IsTrue(result.Success, $"Should succeed. Error: {result.Error}");
            Assert.AreEqual("Box", result.Data.ColliderType);
            Assert.IsNotNull(_testGo.GetComponent<BoxCollider>());
        }

        [Test]
        public void AddCollider_SphereType_AddsSphereCollider()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsAddColliderParams
            {
                Name = "PhysicsTestCube",
                Type = "Sphere"
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsAddColliderTool.Execute(p);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("Sphere", result.Data.ColliderType);
            Assert.IsNotNull(_testGo.GetComponent<SphereCollider>());
        }

        [Test]
        public void AddCollider_InvalidType_ReturnsError()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsAddColliderParams
            {
                Name = "PhysicsTestCube",
                Type = "Cylinder"
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsAddColliderTool.Execute(p);

            Assert.IsFalse(result.Success);
        }

        // L11: a concave MeshCollider on a dynamic Rigidbody is invalid in Unity and silently
        // produces no collision — this must be rejected up front, not shipped broken.
        [Test]
        public void AddCollider_ConcaveMeshWithDynamicRigidbody_ReturnsError()
        {
            Undo.AddComponent<Rigidbody>(_testGo); // non-kinematic by default

            var result = Mosaic.Bridge.Tools.Physics.PhysicsAddColliderTool.Execute(
                new Mosaic.Bridge.Tools.Physics.PhysicsAddColliderParams
                {
                    Name = "PhysicsTestCube", Type = "Mesh", Convex = false
                });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Convex", result.Error);
            Assert.IsNull(_testGo.GetComponent<MeshCollider>(),
                "no MeshCollider should have been left behind on the rejected combination");
        }

        [Test]
        public void AddCollider_ConvexMeshWithDynamicRigidbody_Succeeds()
        {
            Undo.AddComponent<Rigidbody>(_testGo);

            var result = Mosaic.Bridge.Tools.Physics.PhysicsAddColliderTool.Execute(
                new Mosaic.Bridge.Tools.Physics.PhysicsAddColliderParams
                {
                    Name = "PhysicsTestCube", Type = "Mesh", Convex = true
                });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(_testGo.GetComponent<MeshCollider>().convex);
        }

        // L11: renderer.bounds is a world-space AABB, already inflated for a rotated object —
        // mapping that back through the inverse rotation does not undo the inflation. Using the
        // mesh's own local-space bounds (MeshFilter.sharedMesh.bounds) is rotation-independent.
        [Test]
        public void AddCollider_Box_OnRotatedObject_IsNotOversized()
        {
            var existing = _testGo.GetComponent<Collider>();
            if (existing != null) Object.DestroyImmediate(existing);
            _testGo.transform.rotation = Quaternion.Euler(37f, 51f, 19f);
            var localMeshBounds = _testGo.GetComponent<MeshFilter>().sharedMesh.bounds;

            var result = Mosaic.Bridge.Tools.Physics.PhysicsAddColliderTool.Execute(
                new Mosaic.Bridge.Tools.Physics.PhysicsAddColliderParams
                {
                    Name = "PhysicsTestCube", Type = "Box"
                });

            Assert.IsTrue(result.Success, result.Error);
            var box = _testGo.GetComponent<BoxCollider>();
            Assert.AreEqual(localMeshBounds.size.x, box.size.x, 0.001f);
            Assert.AreEqual(localMeshBounds.size.y, box.size.y, 0.001f);
            Assert.AreEqual(localMeshBounds.size.z, box.size.z, 0.001f);
        }

        // ── Overlap ─────────────────────────────────────────────────────────

        [Test]
        public void Overlap_SphereAroundCube_FindsCube()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsOverlapParams
            {
                Action   = "sphere",
                Position = new float[] { 0f, 0f, 0f },
                Radius   = 5f
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsOverlapTool.Execute(p);

            Assert.IsTrue(result.Success, $"Should succeed. Error: {result.Error}");
            Assert.Greater(result.Data.Count, 0, "Should find at least one collider");
        }

        [Test]
        public void Overlap_InvalidAction_ReturnsError()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsOverlapParams
            {
                Action   = "cone",
                Position = new float[] { 0f, 0f, 0f }
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsOverlapTool.Execute(p);

            Assert.IsFalse(result.Success);
        }

        // ── Set Gravity ─────────────────────────────────────────────────────

        [Test]
        public void SetGravity_CustomValue_UpdatesPhysicsGravity()
        {
            var original = UnityEngine.Physics.gravity;

            try
            {
                var p = new Mosaic.Bridge.Tools.Physics.PhysicsSetGravityParams
                {
                    Gravity = new float[] { 0f, -20f, 0f }
                };

                var result = Mosaic.Bridge.Tools.Physics.PhysicsSetGravityTool.Execute(p);

                Assert.IsTrue(result.Success, $"Should succeed. Error: {result.Error}");
                Assert.AreEqual(-20f, result.Data.Gravity[1], 0.01f);
                Assert.AreEqual(-20f, UnityEngine.Physics.gravity.y, 0.01f);
            }
            finally
            {
                // Restore original gravity
                UnityEngine.Physics.gravity = original;
            }
        }

        [Test]
        public void SetGravity_InvalidArray_ReturnsError()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsSetGravityParams
            {
                Gravity = new float[] { 0f, -9.81f }
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsSetGravityTool.Execute(p);

            Assert.IsFalse(result.Success);
        }

        // ── Set PhysicMaterial ──────────────────────────────────────────────

        [Test]
        public void SetPhysicsMaterial_OnCollider_AppliesMaterial()
        {
            var p = new Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialParams
            {
                Name            = "PhysicsTestCube",
                DynamicFriction = 0.3f,
                StaticFriction  = 0.4f,
                Bounciness      = 0.8f
            };

            var result = Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialTool.Execute(p);

            Assert.IsTrue(result.Success, $"Should succeed. Error: {result.Error}");
            Assert.AreEqual(0.3f, result.Data.DynamicFriction, 0.01f);
            Assert.AreEqual(0.4f, result.Data.StaticFriction, 0.01f);
            Assert.AreEqual(0.8f, result.Data.Bounciness, 0.01f);

            var collider = _testGo.GetComponent<Collider>();
            Assert.IsNotNull(collider.sharedMaterial);
        }

        [Test]
        public void SetPhysicsMaterial_NoCollider_ReturnsError()
        {
            // Create a bare GO with no collider
            var bareGo = new GameObject("BareObject");
            try
            {
                var p = new Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialParams
                {
                    Name            = "BareObject",
                    DynamicFriction = 0.5f,
                    StaticFriction  = 0.5f,
                    Bounciness      = 0.5f
                };

                var result = Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialTool.Execute(p);

                Assert.IsFalse(result.Success);
            }
            finally
            {
                Object.DestroyImmediate(bareGo);
            }
        }

        // L10: DynamicFriction/StaticFriction/Bounciness were non-nullable floats, so omitting
        // them meant "force to 0" — an icy floor by accident — instead of keeping Unity's own
        // PhysicsMaterial defaults (0.6 / 0.6 / 0).
        [Test]
        public void SetPhysicsMaterial_OmittedFields_KeepUnityDefaults()
        {
            var result = Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialTool.Execute(
                new Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialParams { Name = "PhysicsTestCube" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.6f, result.Data.DynamicFriction, 0.001f);
            Assert.AreEqual(0.6f, result.Data.StaticFriction, 0.001f);
            Assert.AreEqual(0f, result.Data.Bounciness, 0.001f);
        }

        // L10: there was no way to point two colliders at the SAME PhysicsMaterial asset.
        [Test]
        public void SetPhysicsMaterial_ReuseExisting_SharesTheSameAsset()
        {
            const string path = "Assets/MosaicPhysicsMaterialReuseTest.physicMaterial";
            var other = new GameObject("PhysicsMaterialReuseOther");
            try
            {
                var first = Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialTool.Execute(
                    new Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialParams
                    {
                        Name = "PhysicsTestCube", DynamicFriction = 0.2f, AssetPath = path
                    });
                Assert.IsTrue(first.Success, first.Error);

                other.AddComponent<BoxCollider>();
                var second = Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialTool.Execute(
                    new Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialParams
                    {
                        Name = "PhysicsMaterialReuseOther", ReuseExisting = true, AssetPath = path
                    });
                Assert.IsTrue(second.Success, second.Error);

                Assert.AreSame(
                    _testGo.GetComponent<Collider>().sharedMaterial,
                    other.GetComponent<Collider>().sharedMaterial,
                    "ReuseExisting must attach the SAME asset, not a new one");
            }
            finally
            {
                Object.DestroyImmediate(other);
                if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path) != null)
                    AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void SetPhysicsMaterial_ReuseExisting_MissingAsset_ReturnsError()
        {
            var result = Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialTool.Execute(
                new Mosaic.Bridge.Tools.Physics.PhysicsSetPhysicsMaterialParams
                {
                    Name = "PhysicsTestCube", ReuseExisting = true,
                    AssetPath = "Assets/MosaicNoSuchPhysicsMaterial_zzz.physicsMaterial"
                });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("No PhysicsMaterial asset found", result.Error);
        }
    }
}
