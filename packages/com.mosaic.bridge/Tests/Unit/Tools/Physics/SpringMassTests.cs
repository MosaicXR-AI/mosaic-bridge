using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Physics;

namespace Mosaic.Bridge.Tests.Unit.Tools.Physics
{
    [TestFixture]
    [Category("Unit")]
    public class SpringMassTests
    {
        private GameObject _sourceGo;

        [SetUp]
        public void SetUp()
        {
            // Create a cube in the scene so surface-topology runs have a MeshFilter
            // to analyze. Drop the collider so name-based Find() isn't ambiguous.
            _sourceGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _sourceGo.name = "SpringMass_TestSrc";
            var c = _sourceGo.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        [TearDown]
        public void TearDown()
        {
            if (_sourceGo != null)
            {
                Object.DestroyImmediate(_sourceGo);
                _sourceGo = null;
            }

            // Clean up generated files
            var genPath = "Assets/Generated/Physics/";
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullGenPath = Path.Combine(projectRoot, genPath);
            if (Directory.Exists(fullGenPath))
            {
                Directory.Delete(fullGenPath, true);
                var metaPath = fullGenPath.TrimEnd('/') + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void Create_JellyPreset_ReturnsValidScript()
        {
            var result = PhysicsSpringMassTool.Execute(new PhysicsSpringMassParams
            {
                GameObjectName = "SpringMass_TestSrc",
                Preset         = "jelly",
                Topology       = "surface",
                Name           = "JellyTest"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual("jelly", result.Data.Preset);
            Assert.IsTrue(result.Data.ScriptPath.StartsWith("Assets/Generated/Physics/"));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith("SpringMassSystem_JellyTest.cs"));
            Assert.Greater(result.Data.ParticleCount, 0);
            Assert.Greater(result.Data.SpringCount, 0);

            // Verify file exists on disk
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullPath = Path.Combine(projectRoot, result.Data.ScriptPath);
            Assert.IsTrue(File.Exists(fullPath), $"Script not written to {fullPath}");
        }

        [Test]
        public void Create_ClothPreset_ReturnsValidScript()
        {
            var result = PhysicsSpringMassTool.Execute(new PhysicsSpringMassParams
            {
                GameObjectName = "SpringMass_TestSrc",
                Preset         = "cloth",
                Topology       = "surface",
                Name           = "ClothTest"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("cloth", result.Data.Preset);
            Assert.IsTrue(result.Data.ScriptPath.EndsWith("SpringMassSystem_ClothTest.cs"));
        }

        [Test]
        public void Create_BouncePreset_ReturnsValidScript()
        {
            var result = PhysicsSpringMassTool.Execute(new PhysicsSpringMassParams
            {
                GameObjectName = "SpringMass_TestSrc",
                Preset         = "bounce",
                Topology       = "tetrahedral",
                Name           = "BounceTest"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("bounce", result.Data.Preset);
            Assert.Greater(result.Data.SpringCount, 0);
        }

        [Test]
        public void Create_HairPreset_LatticeTopology_ReturnsValidScript()
        {
            // Lattice topology needs no mesh
            var result = PhysicsSpringMassTool.Execute(new PhysicsSpringMassParams
            {
                Preset   = "hair",
                Topology = "lattice",
                Name     = "HairTest"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("hair", result.Data.Preset);
            Assert.AreEqual(4 * 4 * 4, result.Data.ParticleCount);
            Assert.Greater(result.Data.SpringCount, 0);
        }

        [Test]
        public void Create_InvalidPreset_ReturnsError()
        {
            var result = PhysicsSpringMassTool.Execute(new PhysicsSpringMassParams
            {
                GameObjectName = "SpringMass_TestSrc",
                Preset         = "bouncy-castle",
                Topology       = "surface",
                Name           = "Bad"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_InvalidTopology_ReturnsError()
        {
            var result = PhysicsSpringMassTool.Execute(new PhysicsSpringMassParams
            {
                GameObjectName = "SpringMass_TestSrc",
                Preset         = "jelly",
                Topology       = "hyperdimensional",
                Name           = "Bad"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Create_MissingMeshAndGameObject_SurfaceTopology_ReturnsError()
        {
            var result = PhysicsSpringMassTool.Execute(new PhysicsSpringMassParams
            {
                Preset   = "jelly",
                Topology = "surface",
                Name     = "NoSource"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
