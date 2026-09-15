using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Terrains
{
    // O4 §4.3: detail prototype full config (render mode, instancing, colors, noise, ground-align)
    // + tunable scatter coverage and masked scatter (slope/height/layer) — the prior scatter was
    // a hardcoded 30% coverage with no way to avoid roads/water/cliffs.
    [TestFixture]
    [Category("Terrain")]
    public class TerrainDetailToolTests
    {
        private const string TexturePath = "Assets/MosaicTerrainDetailTestTexture.png";
        private GameObject _terrainGo;
        private string _terrainDataPath;

        [SetUp]
        public void SetUp()
        {
            var created = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_Detail", Width = 50f, Length = 50f, Height = 20f, HeightmapResolution = 33,
            });
            Assert.IsTrue(created.Success, created.Error);
            _terrainGo = UnityIds.Resolve(created.Data.InstanceId) as GameObject;
            _terrainDataPath = created.Data.TerrainDataAssetPath;

            var tex = new Texture2D(4, 4);
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), TexturePath),
                ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            if (!string.IsNullOrEmpty(_terrainDataPath) && AssetDatabase.AssetPathExists(_terrainDataPath))
                AssetDatabase.DeleteAsset(_terrainDataPath);
            if (AssetDatabase.AssetPathExists(TexturePath)) AssetDatabase.DeleteAsset(TexturePath);
        }

        [Test]
        public void AddPrototype_FullConfig_AppliesAllFields()
        {
            var result = TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "add-prototype", TexturePath = TexturePath,
                RenderMode = "Grass", UseInstancing = true,
                HealthyColor = new[] { 0.2f, 0.8f, 0.1f }, DryColor = new[] { 0.6f, 0.5f, 0.1f, 0.5f },
                NoiseSpread = 0.4f, AlignToGround = 0.75f,
            });

            Assert.IsTrue(result.Success, result.Error);
            var data = _terrainGo.GetComponent<Terrain>().terrainData;
            var proto = data.detailPrototypes[0];
            Assert.AreEqual(DetailRenderMode.Grass, proto.renderMode);
            Assert.IsTrue(proto.useInstancing);
            Assert.AreEqual(0.2f, proto.healthyColor.r, 0.001f);
            Assert.AreEqual(1f, proto.healthyColor.a, 0.001f);
            Assert.AreEqual(0.5f, proto.dryColor.a, 0.001f);
            Assert.AreEqual(0.4f, proto.noiseSpread, 0.001f);
            Assert.AreEqual(0.75f, proto.alignToGround, 0.001f);
        }

        [Test]
        public void AddPrototype_UnknownRenderMode_ReturnsInvalidParam()
        {
            var result = TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "add-prototype", TexturePath = TexturePath,
                RenderMode = "Holographic",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetResolution_AppliesToTerrainData()
        {
            var result = TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "set-resolution",
                DetailResolution = 128, ResolutionPerPatch = 32,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(128, result.Data.DetailResolution);
            Assert.AreEqual(32, result.Data.ResolutionPerPatch);
            var data = _terrainGo.GetComponent<Terrain>().terrainData;
            Assert.AreEqual(128, data.detailResolution);
            Assert.AreEqual(32, data.detailResolutionPerPatch);
        }

        [Test]
        public void SetResolution_ZeroOrLess_ReturnsInvalidParam()
        {
            var result = TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "set-resolution",
                DetailResolution = 0, ResolutionPerPatch = 16,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ScatterMode_InstanceCountMode_AppliesToTerrainData()
        {
            var result = TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "scatter-mode", ScatterMode = "InstanceCountMode",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("InstanceCountMode", result.Data.ScatterMode);
            var data = _terrainGo.GetComponent<Terrain>().terrainData;
            Assert.AreEqual(DetailScatterMode.InstanceCountMode, data.detailScatterMode);
        }

        [Test]
        public void ScatterMode_Unknown_ReturnsInvalidParam()
        {
            var result = TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "scatter-mode", ScatterMode = "Chaotic",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Scatter_TunableCoverage_ZeroPlacesNothing()
        {
            TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "set-resolution",
                DetailResolution = 32, ResolutionPerPatch = 8,
            });
            TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "add-prototype", TexturePath = TexturePath,
            });

            var result = TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "scatter", PrototypeIndex = 0,
                ScatterCoverage = 0f, Seed = 1,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.PlacedCount);
        }

        [Test]
        public void Scatter_MaskedByHeight_OnlyPlacesWithinRange()
        {
            var terrain = _terrainGo.GetComponent<Terrain>();
            var res = terrain.terrainData.heightmapResolution;
            var heights = new float[res, res];
            terrain.terrainData.SetHeights(0, 0, heights);

            TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "set-resolution",
                DetailResolution = 32, ResolutionPerPatch = 8,
            });
            TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "add-prototype", TexturePath = TexturePath,
            });

            var result = TerrainDetailTool.Execute(new TerrainDetailParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "scatter", PrototypeIndex = 0,
                ScatterCoverage = 1f, Seed = 1, MinHeightWorld = 1000f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.PlacedCount);
        }
    }
}
