using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Terrains
{
    // O4 §4.3: real-world DEM terrains and hand-painted heightmaps had no import/export route —
    // the Terrain Tools Toolbox equivalent is GUI-only.
    [TestFixture]
    [Category("Terrain")]
    public class TerrainHeightmapToolTests
    {
        private const int Res = 33;
        private const string TexturePath = "Assets/MosaicTestHeightmapTexture.png";
        private GameObject _terrainGo;
        private string _terrainDataPath;
        private string _rawPath;

        [SetUp]
        public void SetUp()
        {
            var created = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_Heightmap", Width = 50f, Length = 50f, Height = 20f, HeightmapResolution = Res,
            });
            Assert.IsTrue(created.Success, created.Error);
            _terrainGo = UnityIds.Resolve(created.Data.InstanceId) as GameObject;
            _terrainDataPath = created.Data.TerrainDataAssetPath;
            _rawPath = Path.Combine(Path.GetTempPath(), "MosaicTestHeightmap.raw");
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            if (!string.IsNullOrEmpty(_terrainDataPath) && AssetDatabase.AssetPathExists(_terrainDataPath))
                AssetDatabase.DeleteAsset(_terrainDataPath);
            if (AssetDatabase.AssetPathExists(TexturePath)) AssetDatabase.DeleteAsset(TexturePath);
            if (File.Exists(_rawPath)) File.Delete(_rawPath);
        }

        [Test]
        public void GetHeights_ReturnsFlatArrayOfResolutionSquared()
        {
            var result = TerrainHeightmapTool.Execute(new TerrainHeightmapParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "get-heights",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(Res, result.Data.Resolution);
            Assert.AreEqual(Res * Res, result.Data.Heights.Length);
        }

        [Test]
        public void ImportTexture_SetsHeightsFromGrayscale()
        {
            var tex = new Texture2D(Res, Res, TextureFormat.RGB24, false);
            var pixels = new Color[Res * Res];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.5f, 0.5f, 0.5f);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(
                Path.Combine(Path.GetDirectoryName(Application.dataPath), TexturePath),
                ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.isReadable = true;
            // 33 isn't a power of two — the importer's default NPOT scaling silently resizes it
            // to 32x32, which would then mismatch heightmapResolution for a reason that has
            // nothing to do with the tool under test.
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();

            var result = TerrainHeightmapTool.Execute(new TerrainHeightmapParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "import-texture", TexturePath = TexturePath,
            });

            Assert.IsTrue(result.Success, result.Error);
            var terrain = _terrainGo.GetComponent<Terrain>();
            var heights = terrain.terrainData.GetHeights(Res / 2, Res / 2, 1, 1);
            Assert.AreEqual(0.5f, heights[0, 0], 0.02f);
        }

        [Test]
        public void ImportTexture_WrongResolution_ReturnsInvalidParam()
        {
            var tex = new Texture2D(4, 4);
            File.WriteAllBytes(
                Path.Combine(Path.GetDirectoryName(Application.dataPath), TexturePath),
                ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);

            var result = TerrainHeightmapTool.Execute(new TerrainHeightmapParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "import-texture", TexturePath = TexturePath,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ExportRaw_ThenImportRaw_RoundTripsHeights()
        {
            // Give the terrain a distinctive height pattern first.
            var terrain = _terrainGo.GetComponent<Terrain>();
            var heights = new float[Res, Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                    heights[y, x] = (x + y) % 2 == 0 ? 0.75f : 0.25f;
            terrain.terrainData.SetHeights(0, 0, heights);

            var exportResult = TerrainHeightmapTool.Execute(new TerrainHeightmapParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "export-raw", RawPath = _rawPath,
            });
            Assert.IsTrue(exportResult.Success, exportResult.Error);
            Assert.IsTrue(File.Exists(_rawPath));
            Assert.AreEqual(Res * Res * 2, new FileInfo(_rawPath).Length);

            // Flatten the terrain, then re-import the exported raw and confirm the pattern returns.
            terrain.terrainData.SetHeights(0, 0, new float[Res, Res]);

            var importResult = TerrainHeightmapTool.Execute(new TerrainHeightmapParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "import-raw", RawPath = _rawPath,
            });
            Assert.IsTrue(importResult.Success, importResult.Error);

            var roundTripped = terrain.terrainData.GetHeights(0, 0, Res, Res);
            Assert.AreEqual(0.75f, roundTripped[0, 0], 0.001f);
            Assert.AreEqual(0.25f, roundTripped[0, 1], 0.001f);
        }

        [Test]
        public void ImportRaw_WrongFileSize_ReturnsInvalidParam()
        {
            File.WriteAllBytes(_rawPath, new byte[10]);

            var result = TerrainHeightmapTool.Execute(new TerrainHeightmapParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "import-raw", RawPath = _rawPath,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void ImportRaw_MissingFile_ReturnsNotFound()
        {
            var result = TerrainHeightmapTool.Execute(new TerrainHeightmapParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "import-raw",
                RawPath = Path.Combine(Path.GetTempPath(), "MosaicNoSuchHeightmap.raw"),
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Execute_UnknownAction_ReturnsInvalidParam()
        {
            var result = TerrainHeightmapTool.Execute(new TerrainHeightmapParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
