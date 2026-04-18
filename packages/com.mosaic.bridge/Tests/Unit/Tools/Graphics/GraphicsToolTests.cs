using NUnit.Framework;
using Mosaic.Bridge.Tools.Graphics;

namespace Mosaic.Bridge.Tests.Unit.Tools.Graphics
{
    [TestFixture]
    [Category("Unit")]
    [Category("Graphics")]
    public class GraphicsToolTests
    {
        [Test]
        public void RenderInfo_ReturnsValidData()
        {
            var result = GraphicsRenderInfoTool.RenderInfo(new GraphicsRenderInfoParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.RenderPipeline, "RenderPipeline should not be null");
            Assert.IsNotNull(result.Data.ColorSpace, "ColorSpace should not be null");
            Assert.IsNotNull(result.Data.GraphicsApi, "GraphicsApi should not be null");
            Assert.IsNotNull(result.Data.QualityLevel, "QualityLevel should not be null");
            Assert.IsNotNull(result.Data.GraphicsDeviceName, "GraphicsDeviceName should not be null");
            Assert.Greater(result.Data.GraphicsMemorySize, 0, "GraphicsMemorySize should be > 0");
        }

        [Test]
        public void SetShader_NonexistentMaterial_ReturnsFail()
        {
            var result = GraphicsSetShaderTool.SetShader(new GraphicsSetShaderParams
            {
                MaterialPath = "Assets/nonexistent.mat",
                ShaderName = "Standard"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void SetPostProcessing_NonexistentGameObject_ReturnsFail()
        {
            var result = GraphicsSetPostProcessingTool.SetPostProcessing(
                new GraphicsSetPostProcessingParams
                {
                    InstanceId = -999999,
                    Name = null
                });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}
