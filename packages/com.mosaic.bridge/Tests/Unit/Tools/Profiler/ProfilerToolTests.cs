using NUnit.Framework;
using Mosaic.Bridge.Tools.Profiling;

namespace Mosaic.Bridge.Tests.Unit.Tools.Profiler
{
    [TestFixture]
    [Category("Unit")]
    [Category("Profiler")]
    public class ProfilerToolTests
    {
        [Test]
        public void Stats_ReturnsNonZeroMemoryValues()
        {
            var result = ProfilerStatsTool.Stats(new ProfilerStatsParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.Greater(result.Data.TotalAllocatedMemory, 0,
                "TotalAllocatedMemory should be > 0");
            Assert.Greater(result.Data.TotalReservedMemory, 0,
                "TotalReservedMemory should be > 0");
            Assert.Greater(result.Data.MonoHeapSize, 0,
                "MonoHeapSize should be > 0");
            Assert.Greater(result.Data.MonoUsedSize, 0,
                "MonoUsedSize should be > 0");
        }

        [Test]
        public void Memory_ReturnsAreas()
        {
            var result = ProfilerMemoryTool.Memory(new ProfilerMemoryParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.Areas);
            Assert.Greater(result.Data.Areas.Count, 0, "Should return at least one memory area");
            Assert.Greater(result.Data.TotalAllocatedMemory, 0);
        }

        [Test]
        public void FrameData_ReturnsValidData()
        {
            var result = ProfilerFrameDataTool.FrameData(new ProfilerFrameDataParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.GreaterOrEqual(result.Data.RealtimeSinceStartup, 0f);
            Assert.GreaterOrEqual(result.Data.FrameCount, 0);
        }

        [Test]
        public void Enable_InvalidAction_ReturnsFail()
        {
            var result = ProfilerEnableTool.Enable(new ProfilerEnableParams
            {
                Action = "invalid-action"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}
