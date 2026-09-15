using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Terrains
{
    // O4 §4.3: terrain material template + GPU instancing + tree LOD bias + rendering layer mask —
    // the last Terrain item before §4.3 is complete.
    [TestFixture]
    [Category("Terrain")]
    public class TerrainSettingsToolTests
    {
        private const string MaterialPath = "Assets/MosaicTerrainSettingsTestMaterial.mat";
        private GameObject _terrainGo;
        private string _terrainDataPath;

        [SetUp]
        public void SetUp()
        {
            var created = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_Settings", Width = 50f, Length = 50f, Height = 20f, HeightmapResolution = 33,
            });
            Assert.IsTrue(created.Success, created.Error);
            _terrainGo = UnityIds.Resolve(created.Data.InstanceId) as GameObject;
            _terrainDataPath = created.Data.TerrainDataAssetPath;

            var material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            if (!string.IsNullOrEmpty(_terrainDataPath) && AssetDatabase.AssetPathExists(_terrainDataPath))
                AssetDatabase.DeleteAsset(_terrainDataPath);
            if (AssetDatabase.AssetPathExists(MaterialPath)) AssetDatabase.DeleteAsset(MaterialPath);
        }

        [Test]
        public void SetMaterialTemplate_AssignsMaterialAsset()
        {
            var result = TerrainSettingsTool.Execute(new TerrainSettingsParams
            {
                InstanceId = UnityIds.Of(_terrainGo), MaterialTemplatePath = MaterialPath,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(MaterialPath, result.Data.MaterialTemplatePath);
            Assert.IsNotNull(_terrainGo.GetComponent<Terrain>().materialTemplate);
        }

        [Test]
        public void SetMaterialTemplate_EmptyString_RevertsToDefault()
        {
            TerrainSettingsTool.Execute(new TerrainSettingsParams
            {
                InstanceId = UnityIds.Of(_terrainGo), MaterialTemplatePath = MaterialPath,
            });

            var result = TerrainSettingsTool.Execute(new TerrainSettingsParams
            {
                InstanceId = UnityIds.Of(_terrainGo), MaterialTemplatePath = "",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNull(_terrainGo.GetComponent<Terrain>().materialTemplate);
        }

        [Test]
        public void SetMaterialTemplate_NotFound_ReturnsNotFound()
        {
            var result = TerrainSettingsTool.Execute(new TerrainSettingsParams
            {
                InstanceId = UnityIds.Of(_terrainGo), MaterialTemplatePath = "Assets/DoesNotExist.mat",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void SetDrawInstanced_TreeLodBias_RenderingLayerMask_AppliesAll()
        {
            var result = TerrainSettingsTool.Execute(new TerrainSettingsParams
            {
                InstanceId = UnityIds.Of(_terrainGo),
                DrawInstanced = true, TreeLodBiasMultiplier = 2.5f, RenderingLayerMask = 5u,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.DrawInstanced);
            Assert.AreEqual(2.5f, result.Data.TreeLodBiasMultiplier, 0.001f);
            Assert.AreEqual(5u, result.Data.RenderingLayerMask);

            var terrain = _terrainGo.GetComponent<Terrain>();
            Assert.IsTrue(terrain.drawInstanced);
            Assert.AreEqual(2.5f, terrain.treeLODBiasMultiplier, 0.001f);
            Assert.AreEqual(5u, terrain.renderingLayerMask);
        }
    }
}
