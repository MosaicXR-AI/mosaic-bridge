using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Materials;

namespace Mosaic.Bridge.Tests.Materials
{
    // O4 §4.3: the _EMISSION keyword alone does not make emissive geometry contribute to a bake —
    // Material.globalIlluminationFlags (BakedEmissive/RealtimeEmissive) is also required.
    [TestFixture]
    [Category("Materials")]
    public class MaterialSetPropertyGiFlagsTests
    {
        private const string MaterialPath = "Assets/MosaicTestGiFlagsMaterial.mat";

        [SetUp]
        public void SetUp()
        {
            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
                AssetDatabase.DeleteAsset(MaterialPath);
        }

        [Test]
        public void SetProperty_GiFlags_BakedEmissive_Applies()
        {
            var result = MaterialSetPropertyTool.Execute(new MaterialSetPropertyParams
            {
                Path = MaterialPath, ValueType = "gi-flags", StringValue = "BakedEmissive",
            });

            Assert.IsTrue(result.Success, result.Error);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.AreEqual(MaterialGlobalIlluminationFlags.BakedEmissive, mat.globalIlluminationFlags);
        }

        [Test]
        public void SetProperty_GiFlags_MultipleFlagsOred()
        {
            var result = MaterialSetPropertyTool.Execute(new MaterialSetPropertyParams
            {
                Path = MaterialPath, ValueType = "gi-flags", StringValue = "BakedEmissive,EmissiveIsBlack",
            });

            Assert.IsTrue(result.Success, result.Error);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.IsTrue((mat.globalIlluminationFlags & MaterialGlobalIlluminationFlags.BakedEmissive) != 0);
            Assert.IsTrue((mat.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) != 0);
        }

        [Test]
        public void SetProperty_GiFlags_UnknownFlag_ReturnsInvalidParam()
        {
            var result = MaterialSetPropertyTool.Execute(new MaterialSetPropertyParams
            {
                Path = MaterialPath, ValueType = "gi-flags", StringValue = "Bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
