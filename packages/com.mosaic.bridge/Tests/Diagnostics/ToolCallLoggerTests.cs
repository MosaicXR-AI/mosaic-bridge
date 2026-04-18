using System.Linq;
using Mosaic.Bridge.Core.Diagnostics;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Diagnostics
{
    [TestFixture]
    public class ToolCallLoggerTests
    {
        [SetUp]
        public void SetUp()
        {
            ToolCallLogger.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ToolCallLogger.Clear();
        }

        [Test]
        public void Record_AddsEntry()
        {
            ToolCallLogger.Record("scene/get_hierarchy", 200, 42.5);

            var records = ToolCallLogger.GetRecords();
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual("scene/get_hierarchy", records[0].ToolName);
            Assert.AreEqual(200, records[0].StatusCode);
            Assert.AreEqual(42.5, records[0].DurationMs, 0.01);
            Assert.IsTrue(records[0].IsSuccess);
            Assert.IsNull(records[0].ErrorCode);
        }

        [Test]
        public void Record_RingBufferCapsAt200()
        {
            for (int i = 0; i < 210; i++)
                ToolCallLogger.Record($"tool_{i}", 200, 1.0);

            var records = ToolCallLogger.GetRecords(300);
            Assert.AreEqual(200, records.Count);
            // Oldest should be tool_10 (first 10 were evicted)
            Assert.AreEqual("tool_10", records[0].ToolName);
            Assert.AreEqual("tool_209", records[records.Count - 1].ToolName);
        }

        [Test]
        public void GetRecords_ReturnsNewest()
        {
            for (int i = 0; i < 10; i++)
                ToolCallLogger.Record($"tool_{i}", 200, 1.0);

            var records = ToolCallLogger.GetRecords(3);
            Assert.AreEqual(3, records.Count);
            Assert.AreEqual("tool_7", records[0].ToolName);
            Assert.AreEqual("tool_8", records[1].ToolName);
            Assert.AreEqual("tool_9", records[2].ToolName);
        }

        [Test]
        public void GetSummary_CalculatesErrorRate()
        {
            ToolCallLogger.Record("tool_a", 200, 10.0);
            ToolCallLogger.Record("tool_b", 200, 10.0);
            ToolCallLogger.Record("tool_c", 500, 10.0, "INTERNAL_ERROR");
            ToolCallLogger.Record("tool_d", 404, 10.0, "NOT_FOUND");

            var summary = ToolCallLogger.GetSummary();
            Assert.AreEqual(4, summary.TotalCalls);
            Assert.AreEqual(2, summary.SuccessCount);
            Assert.AreEqual(2, summary.FailureCount);
            Assert.AreEqual(50.0, summary.ErrorRate, 0.01);
        }

        [Test]
        public void GetSummary_CalculatesAvgDuration()
        {
            ToolCallLogger.Record("tool_a", 200, 10.0);
            ToolCallLogger.Record("tool_b", 200, 20.0);
            ToolCallLogger.Record("tool_c", 200, 30.0);

            var summary = ToolCallLogger.GetSummary();
            Assert.AreEqual(20.0, summary.AverageDurationMs, 0.01);
        }

        [Test]
        public void Clear_RemovesAll()
        {
            ToolCallLogger.Record("tool_a", 200, 5.0);
            ToolCallLogger.Record("tool_b", 500, 10.0);
            Assert.AreEqual(2, ToolCallLogger.GetRecords().Count);

            ToolCallLogger.Clear();

            Assert.AreEqual(0, ToolCallLogger.GetRecords().Count);
            var summary = ToolCallLogger.GetSummary();
            Assert.AreEqual(0, summary.TotalCalls);
            Assert.AreEqual(0, summary.AverageDurationMs, 0.01);
            Assert.AreEqual(0, summary.ErrorRate, 0.01);
        }
    }
}
