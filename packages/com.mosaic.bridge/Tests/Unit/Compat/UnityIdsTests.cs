using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Unit.Compat
{
    /// <summary>
    /// Guards the Unity object-identity compatibility shim. Unity 6.5 made the old
    /// 32-bit instance ID API obsolete-as-error, so these tests pin the behaviour the
    /// rest of the bridge (and the MCP wire format) depends on: ids are non-zero,
    /// stable, unique per object, and round-trip back to the same object.
    /// </summary>
    [TestFixture]
    public class UnityIdsTests
    {
        // ── Of ───────────────────────────────────────────────────────────────

        [Test]
        public void Of_ReturnsNonZeroId()
        {
            var go = new GameObject("UnityIds_NonZero");
            try
            {
                Assert.AreNotEqual(0, UnityIds.Of(go));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Of_IsStableAcrossCalls()
        {
            var go = new GameObject("UnityIds_Stable");
            try
            {
                Assert.AreEqual(UnityIds.Of(go), UnityIds.Of(go));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Of_IsUniquePerObject()
        {
            var a = new GameObject("UnityIds_UniqueA");
            var b = new GameObject("UnityIds_UniqueB");
            try
            {
                Assert.AreNotEqual(UnityIds.Of(a), UnityIds.Of(b));
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void Of_DistinguishesComponentFromItsGameObject()
        {
            var go = new GameObject("UnityIds_Component");
            try
            {
                var cam = go.AddComponent<UnityEngine.Camera>();
                Assert.AreNotEqual(UnityIds.Of(go), UnityIds.Of(cam));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── Resolve ──────────────────────────────────────────────────────────

        [Test]
        public void Resolve_RoundTripsToSameObject()
        {
            var go = new GameObject("UnityIds_RoundTrip");
            try
            {
                Assert.AreSame(go, UnityIds.Resolve(UnityIds.Of(go)));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Resolve_RoundTripsAComponent()
        {
            var go = new GameObject("UnityIds_RoundTripComponent");
            try
            {
                var cam = go.AddComponent<UnityEngine.Camera>();
                Assert.AreSame(cam, UnityIds.Resolve(UnityIds.Of(cam)));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Resolve_ReturnsNullForUnknownId()
        {
            Assert.IsNull(UnityIds.Resolve(0));
        }

        [Test]
        public void ResolveAny_RoundTripsToSameObject()
        {
            var go = new GameObject("UnityIds_ResolveAny");
            try
            {
                Assert.AreSame(go, UnityIds.ResolveAny(UnityIds.Of(go)));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── FindAll ──────────────────────────────────────────────────────────

        [Test]
        public void FindAll_FindsAnActiveObject()
        {
            var go = new GameObject("UnityIds_FindAll");
            try
            {
                go.AddComponent<EventSystem>();
                var found = UnityIds.FindAll<EventSystem>();
                Assert.IsTrue(System.Array.Exists(found, e => e.gameObject == go));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FindAll_ExcludesInactiveObjects()
        {
            var go = new GameObject("UnityIds_FindAllInactive");
            try
            {
                go.AddComponent<EventSystem>();
                go.SetActive(false);
                var found = UnityIds.FindAll<EventSystem>();
                Assert.IsFalse(System.Array.Exists(found, e => e.gameObject == go));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── EntityId layout invariant (Unity 6.5+) ───────────────────────────

#if UNITY_6000_5_OR_NEWER
        /// <summary>
        /// <see cref="UnityIds.Resolve"/> rebuilds a 64-bit EntityId from a 32-bit id by
        /// reusing the session's shared high 32 bits, because Unity exposes no public
        /// int-to-EntityId conversion. If a future Unity starts varying those high bits,
        /// this test fails — and the id must widen to 64 bits on the MCP wire.
        /// </summary>
        [Test]
        public void EntityId_HighBitsAreSharedAcrossObjectKinds()
        {
            var objs = new System.Collections.Generic.List<Object>();
            var gos  = new System.Collections.Generic.List<GameObject>();
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    var go = new GameObject("UnityIds_HighBits" + i);
                    gos.Add(go);
                    objs.Add(go);
                    objs.Add(go.AddComponent<UnityEngine.Camera>());
                }
                objs.Add(Shader.Find("Standard"));                          // built-in asset
                objs.Add(ScriptableObject.CreateInstance<ScriptableObject>()); // loose object

                var highBits = new System.Collections.Generic.HashSet<ulong>();
                foreach (var o in objs)
                {
                    if (o == null) continue;
                    highBits.Add(EntityId.ToULong(o.GetEntityId()) & 0xFFFFFFFF00000000UL);
                }

                Assert.AreEqual(1, highBits.Count,
                    "EntityId high bits are no longer shared across objects — the 32-bit " +
                    "InstanceId wire format can no longer round-trip and must widen to 64 bits.");
            }
            finally
            {
                foreach (var go in gos) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Resolve_RoundTripsManyObjects()
        {
            var gos = new System.Collections.Generic.List<GameObject>();
            try
            {
                for (int i = 0; i < 50; i++)
                    gos.Add(new GameObject("UnityIds_Many" + i));

                foreach (var go in gos)
                    Assert.AreSame(go, UnityIds.Resolve(UnityIds.Of(go)));
            }
            finally
            {
                foreach (var go in gos) Object.DestroyImmediate(go);
            }
        }
#endif

        // ── ObjectReferenceId ────────────────────────────────────────────────

        [Test]
        public void ObjectReferenceId_IsZeroForUnassignedReference()
        {
            var go = new GameObject("UnityIds_ObjRefEmpty");
            try
            {
                var probe = go.AddComponent<UnityIdsProbe>();
                var prop  = new SerializedObject(probe).FindProperty("Target");

                Assert.IsNotNull(prop);
                Assert.AreEqual(0, UnityIds.ObjectReferenceId(prop));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ObjectReferenceId_MatchesTargetIdWhenAssigned()
        {
            var go     = new GameObject("UnityIds_ObjRefSet");
            var target = new GameObject("UnityIds_ObjRefTarget");
            try
            {
                var probe = go.AddComponent<UnityIdsProbe>();
                probe.Target = target;

                var prop = new SerializedObject(probe).FindProperty("Target");

                Assert.IsNotNull(prop);
                Assert.AreEqual(UnityIds.Of(target), UnityIds.ObjectReferenceId(prop));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
            }
        }
    }

    /// <summary>Serialized object-reference field, for the ObjectReferenceId tests.</summary>
    internal class UnityIdsProbe : MonoBehaviour
    {
        public GameObject Target;
    }
}
