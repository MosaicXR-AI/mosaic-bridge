using NUnit.Framework;
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
    }
}
