using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.DataViz;

namespace Mosaic.Bridge.Tests.Unit.Tools.DataViz
{
    [TestFixture]
    [Category("Unit")]
    public class ExplodedViewTests
    {
        private GameObject _root;
        private readonly List<GameObject> _extra = new List<GameObject>();
        private readonly string _savePath = "Assets/Generated/DataViz_Tests_Explode/";

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ExplodeRootGO");
            _root.transform.position = Vector3.zero;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (var go in _extra)
                if (go != null) Object.DestroyImmediate(go);
            _extra.Clear();
            _root = null;

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var dir = Path.Combine(projectRoot, _savePath);
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); } catch { /* best-effort */ }
                var metaFile = dir.TrimEnd('/') + ".meta";
                if (File.Exists(metaFile))
                {
                    try { File.Delete(metaFile); } catch { /* best-effort */ }
                }
            }
        }

        // Creates N direct children of _root arranged around origin with small offsets.
        private Transform[] MakeChildren(int count, float radius = 2f)
        {
            var children = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.name = $"Part_{i}";
                child.transform.SetParent(_root.transform, worldPositionStays: true);
                // Place on a ring in XZ plane so radial direction is well-defined.
                float angle = (i / (float)count) * Mathf.PI * 2f;
                child.transform.position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                children[i] = child.transform;
            }
            return children;
        }

        private ViewExplodeParams BaseParams()
        {
            return new ViewExplodeParams
            {
                RootGameObject = "ExplodeRootGO",
                Strategy       = "direct_children",
                Direction      = "radial",
                ExplosionFactor= 1f,
                Animate        = false,
                SavePath       = _savePath
            };
        }

        // ---------------- Strategy tests ----------------

        [Test]
        public void DirectChildren_Radial_ExplodesAllChildren()
        {
            var children = MakeChildren(4);
            var originals = new Vector3[children.Length];
            for (int i = 0; i < children.Length; i++) originals[i] = children[i].position;

            var p = BaseParams();
            var r = ViewExplodeTool.Execute(p);

            Assert.IsTrue(r.Success, r.Error);
            Assert.AreEqual(children.Length, r.Data.AffectedPartCount);
            Assert.AreEqual("direct_children", r.Data.Strategy);

            for (int i = 0; i < children.Length; i++)
            {
                Assert.Greater(Vector3.Distance(children[i].position, originals[i]), 0.01f,
                    $"Part {i} did not move");
                // Outward from origin: exploded distance must exceed original distance.
                Assert.Greater(children[i].position.magnitude, originals[i].magnitude - 0.001f);
            }
        }

        [Test]
        public void AllRenderers_FindsDeepChildren()
        {
            // Create a nested hierarchy: root -> mid (no renderer) -> leaf (cube w/ renderer).
            var mid = new GameObject("Mid");
            mid.transform.SetParent(_root.transform);
            mid.transform.position = new Vector3(3f, 0f, 0f);

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Leaf";
            leaf.transform.SetParent(mid.transform, worldPositionStays: true);
            leaf.transform.position = new Vector3(3f, 0f, 0f);

            // Also add a direct-child cube so we have multiple renderers.
            var direct = GameObject.CreatePrimitive(PrimitiveType.Cube);
            direct.name = "Direct";
            direct.transform.SetParent(_root.transform, worldPositionStays: true);
            direct.transform.position = new Vector3(-3f, 0f, 0f);

            var p = BaseParams();
            p.Strategy = "all_renderers";
            var r = ViewExplodeTool.Execute(p);

            Assert.IsTrue(r.Success, r.Error);
            // At minimum the deep leaf and the direct renderer are found.
            Assert.GreaterOrEqual(r.Data.AffectedPartCount, 2);
            Assert.AreEqual("all_renderers", r.Data.Strategy);
        }

        [Test]
        public void ByLayer_GroupsCorrectly()
        {
            // Three children on two different layers.
            var children = MakeChildren(3);
            children[0].gameObject.layer = 0; // Default
            children[1].gameObject.layer = 0;
            children[2].gameObject.layer = 5; // UI (always present)

            var p = BaseParams();
            p.Strategy = "by_layer";
            var r = ViewExplodeTool.Execute(p);

            Assert.IsTrue(r.Success, r.Error);
            Assert.AreEqual(3, r.Data.AffectedPartCount);
            Assert.AreEqual("by_layer", r.Data.Strategy);
        }

        // ---------------- ExplosionFactor tests ----------------

        [Test]
        public void ExplosionFactor_Zero_NoMovement()
        {
            var children = MakeChildren(3);
            var originals = new Vector3[children.Length];
            for (int i = 0; i < children.Length; i++) originals[i] = children[i].position;

            var p = BaseParams();
            p.ExplosionFactor = 0f;
            var r = ViewExplodeTool.Execute(p);

            Assert.IsTrue(r.Success, r.Error);
            for (int i = 0; i < children.Length; i++)
            {
                Assert.AreEqual(originals[i].x, children[i].position.x, 1e-4f);
                Assert.AreEqual(originals[i].y, children[i].position.y, 1e-4f);
                Assert.AreEqual(originals[i].z, children[i].position.z, 1e-4f);
            }
        }

        [Test]
        public void ExplosionFactor_Two_MovesMoreThanFactorOne()
        {
            // First pass at factor 1.
            var children1 = MakeChildren(3);
            var originals = new Vector3[children1.Length];
            for (int i = 0; i < children1.Length; i++) originals[i] = children1[i].position;

            var p1 = BaseParams();
            p1.ExplosionFactor = 1f;
            var r1 = ViewExplodeTool.Execute(p1);
            Assert.IsTrue(r1.Success, r1.Error);
            var at1 = new Vector3[children1.Length];
            for (int i = 0; i < children1.Length; i++) at1[i] = children1[i].position;

            // Reset + second pass at factor 2 by rebuilding the hierarchy.
            Object.DestroyImmediate(_root);
            _root = new GameObject("ExplodeRootGO");
            var children2 = MakeChildren(3);

            var p2 = BaseParams();
            p2.ExplosionFactor = 2f;
            var r2 = ViewExplodeTool.Execute(p2);
            Assert.IsTrue(r2.Success, r2.Error);

            for (int i = 0; i < children2.Length; i++)
            {
                float d1 = Vector3.Distance(at1[i], originals[i]);
                float d2 = Vector3.Distance(children2[i].position, originals[i]);
                Assert.Greater(d2, d1 - 1e-4f, $"Factor 2 should displace >= factor 1 for part {i}");
            }
        }

        // ---------------- Validation tests ----------------

        [Test]
        public void MissingRootGameObject_ReturnsError()
        {
            var r = ViewExplodeTool.Execute(new ViewExplodeParams { RootGameObject = null });
            Assert.IsFalse(r.Success);
            Assert.AreEqual("INVALID_PARAM", r.ErrorCode);
        }

        [Test]
        public void RootGameObject_NotFound_ReturnsNotFound()
        {
            var r = ViewExplodeTool.Execute(new ViewExplodeParams
            {
                RootGameObject = "NonExistent_Root_9999"
            });
            Assert.IsFalse(r.Success);
            Assert.AreEqual("NOT_FOUND", r.ErrorCode);
        }

        [Test]
        public void InvalidStrategy_ReturnsError()
        {
            MakeChildren(2);
            var p = BaseParams();
            p.Strategy = "galactic";
            var r = ViewExplodeTool.Execute(p);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("INVALID_PARAM", r.ErrorCode);
        }

        [Test]
        public void InvalidDirection_ReturnsError()
        {
            MakeChildren(2);
            var p = BaseParams();
            p.Direction = "diagonal_blast";
            var r = ViewExplodeTool.Execute(p);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("INVALID_PARAM", r.ErrorCode);
        }

        // ---------------- Animate tests ----------------

        [Test]
        public void Animate_True_GeneratesScriptAndReturnsPath()
        {
            MakeChildren(3);
            var p = BaseParams();
            p.Animate = true;
            p.Duration = 2f;

            var r = ViewExplodeTool.Execute(p);
            Assert.IsTrue(r.Success, r.Error);
            Assert.IsNotNull(r.Data.ScriptPath);
            Assert.IsTrue(r.Data.ScriptPath.EndsWith("ExplodedViewAnimator.cs"), r.Data.ScriptPath);

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, r.Data.ScriptPath);
            Assert.IsTrue(File.Exists(fullPath), $"Animator script missing at {fullPath}");
        }

        // ---------------- Axis-constrained tests ----------------

        [Test]
        public void AxisX_KeepsYAndZUnchanged()
        {
            // Arrange children off the Y/Z axes so any leakage would show up.
            var c0 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c0.name = "Px";
            c0.transform.SetParent(_root.transform, worldPositionStays: true);
            c0.transform.position = new Vector3(2f, 1.5f, 3f);

            var c1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c1.name = "Nx";
            c1.transform.SetParent(_root.transform, worldPositionStays: true);
            c1.transform.position = new Vector3(-2f, -0.75f, -1.25f);

            var origY0 = c0.transform.position.y;
            var origZ0 = c0.transform.position.z;
            var origY1 = c1.transform.position.y;
            var origZ1 = c1.transform.position.z;

            var p = BaseParams();
            p.Direction = "axis_x";
            p.ExplosionFactor = 1f;

            var r = ViewExplodeTool.Execute(p);
            Assert.IsTrue(r.Success, r.Error);

            Assert.AreEqual(origY0, c0.transform.position.y, 1e-4f, "Y should be unchanged on axis_x");
            Assert.AreEqual(origZ0, c0.transform.position.z, 1e-4f, "Z should be unchanged on axis_x");
            Assert.AreEqual(origY1, c1.transform.position.y, 1e-4f, "Y should be unchanged on axis_x");
            Assert.AreEqual(origZ1, c1.transform.position.z, 1e-4f, "Z should be unchanged on axis_x");

            // And X must have moved.
            Assert.AreNotEqual(2f, c0.transform.position.x);
            Assert.AreNotEqual(-2f, c1.transform.position.x);
        }
    }
}
