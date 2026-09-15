using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Lighting;

namespace Mosaic.Bridge.Tests.Lighting
{
    // O4 §4.3: metallic/glass surfaces need reflection probes to look right in a capture.
    [TestFixture]
    [Category("Lighting")]
    public class LightingReflectionProbeTests
    {
        private const string ProbeName = "__MosaicTest_ReflectionProbe__";
        private const string BakePath = "Assets/MosaicTestReflectionProbe.exr";

        [TearDown]
        public void TearDown()
        {
            var go = GameObject.Find(ProbeName);
            if (go != null) Object.DestroyImmediate(go);
            if (AssetDatabase.LoadAssetAtPath<Texture>(BakePath) != null)
                AssetDatabase.DeleteAsset(BakePath);
        }

        [Test]
        public void Create_AppliesFields()
        {
            var result = LightingReflectionProbeTool.Execute(new LightingReflectionProbeParams
            {
                Action = "create", Name = ProbeName, Position = new[] { 1f, 2f, 3f },
                Mode = "Baked", Resolution = 256, Size = new[] { 5f, 5f, 5f }, BoxProjection = true,
                Hdr = true, Intensity = 1.5f, Importance = 2,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Baked", result.Data.Mode);
            Assert.AreEqual(256, result.Data.Resolution);
            Assert.AreEqual(1.5f, result.Data.Intensity, 0.0001f);

            var go = GameObject.Find(ProbeName);
            var probe = go.GetComponent<ReflectionProbe>();
            Assert.AreEqual(new Vector3(5, 5, 5), probe.size);
            Assert.IsTrue(probe.boxProjection);
            Assert.IsTrue(probe.hdr);
            Assert.AreEqual(2, probe.importance);
        }

        [Test]
        public void Set_UpdatesExistingProbe()
        {
            LightingReflectionProbeTool.Execute(new LightingReflectionProbeParams
            {
                Action = "create", Name = ProbeName,
            });

            var result = LightingReflectionProbeTool.Execute(new LightingReflectionProbeParams
            {
                Action = "set", Name = ProbeName, Intensity = 3f, BlendDistance = 2f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3f, result.Data.Intensity, 0.0001f);
            var probe = GameObject.Find(ProbeName).GetComponent<ReflectionProbe>();
            Assert.AreEqual(2f, probe.blendDistance, 0.0001f);
        }

        [Test]
        public void Bake_ProducesACubemapAsset()
        {
            LightingReflectionProbeTool.Execute(new LightingReflectionProbeParams
            {
                Action = "create", Name = ProbeName, Mode = "Baked",
            });

            var result = LightingReflectionProbeTool.Execute(new LightingReflectionProbeParams
            {
                Action = "bake", Name = ProbeName, BakePath = BakePath,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.BakeSuccess);
            Assert.AreEqual(BakePath, result.Data.BakePath);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture>(BakePath));
        }

        [Test]
        public void BakeAll_BakesEveryProbeInScene()
        {
            LightingReflectionProbeTool.Execute(new LightingReflectionProbeParams
            {
                Action = "create", Name = ProbeName, Mode = "Baked",
            });

            var result = LightingReflectionProbeTool.Execute(new LightingReflectionProbeParams
            {
                Action = "bake-all",
            });

            try
            {
                Assert.IsTrue(result.Success, result.Error);
                Assert.IsTrue(result.Data.BakedProbes.Any(b => b.Name == ProbeName && b.Success));
            }
            finally
            {
                foreach (var baked in result.Data.BakedProbes)
                    if (AssetDatabase.LoadAssetAtPath<Texture>(baked.BakePath) != null)
                        AssetDatabase.DeleteAsset(baked.BakePath);
            }
        }

        [Test]
        public void Set_ProbeNotFound_ReturnsNotFound()
        {
            var result = LightingReflectionProbeTool.Execute(new LightingReflectionProbeParams
            {
                Action = "set", Name = "NoSuchProbe", Intensity = 1f,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Create_UnknownMode_ReturnsInvalidParam()
        {
            var result = LightingReflectionProbeTool.Execute(new LightingReflectionProbeParams
            {
                Action = "create", Name = ProbeName, Mode = "Bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            Assert.IsNull(GameObject.Find(ProbeName), "a failed create must not leave a partial GameObject behind");
        }
    }
}
