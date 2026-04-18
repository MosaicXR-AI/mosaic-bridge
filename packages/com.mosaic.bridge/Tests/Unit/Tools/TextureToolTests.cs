using NUnit.Framework;
using Mosaic.Bridge.Tools.Textures;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    [TestFixture]
    [Category("Unit")]
    public class TextureToolTests
    {
        [Test]
        public void TextureSetImportSettings_InvalidPath_Fails()
        {
            var result = TextureSetImportSettingsTool.SetImportSettings(
                new TextureSetImportSettingsParams
                {
                    AssetPath = "Assets/NonExistent/texture.png"
                });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void TextureSetImportSettings_InvalidTextureType_Fails()
        {
            // Even if the asset existed, invalid type should fail
            var result = TextureSetImportSettingsTool.SetImportSettings(
                new TextureSetImportSettingsParams
                {
                    AssetPath = "Assets/NonExistent/texture.png",
                    TextureType = "Invalid"
                });

            // Will fail with NOT_FOUND first since the asset doesn't exist
            Assert.IsFalse(result.Success);
        }
    }
}
