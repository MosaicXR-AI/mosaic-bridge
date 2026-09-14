using System;
using Mosaic.Bridge.Core.Jobs;
using Mosaic.Bridge.Tools.Build;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Unit.Tools.PlayerBuild
{
    /// <summary>
    /// O4 §3.4: build/start must return immediately instead of blocking the HTTP request for a
    /// player build's entire duration (the "45-minute hang", backlog 2026-08-26). These tests
    /// cover BuildJobKind.Probe's state-derivation logic directly — actually invoking
    /// BuildPipeline.BuildPlayer (the Reports-branch) is deliberately NOT unit-tested: a real
    /// player build is far too slow and disk-heavy to run per-test, and is exercised instead by
    /// build/build's own manual/CI verification.
    /// </summary>
    public class BuildJobKindTests
    {
        private readonly BuildJobKind _kind = new BuildJobKind();

        private static JobRecord MakeRecord(string jobId, DateTime? startedUtc = null) => new JobRecord
        {
            JobId = jobId,
            Kind = BuildJobsBootstrap.Kind,
            StartedUtc = (startedUtc ?? DateTime.UtcNow).ToString("o"),
            Status = JobStatus.Running,
        };

        [Test]
        public void Probe_Pending_ReportsRunning()
        {
            var jobId = Guid.NewGuid().ToString("N");
            BuildJobKind.RegisterPendingForTest(jobId);
            var record = MakeRecord(jobId);

            _kind.Probe(record);

            Assert.AreEqual(JobStatus.Running, record.Status);
        }

        [Test]
        public void Probe_NeitherPendingNorReported_NoBuildReportEvidence_StaysRunning()
        {
            // A job id that was never registered anywhere and predates any build this Editor
            // session might have produced — Library/LastBuild.buildreport (if it exists at all)
            // will not be newer than "now", so this must not falsely report Done.
            var jobId = Guid.NewGuid().ToString("N");
            var record = MakeRecord(jobId, DateTime.UtcNow.AddYears(1)); // StartedUtc in the future

            _kind.Probe(record);

            Assert.AreEqual(JobStatus.Running, record.Status);
            StringAssert.Contains("domain reload", record.Message);
        }

        [Test]
        public void Cancel_PendingJob_RemovesItAndReturnsTrue()
        {
            var jobId = Guid.NewGuid().ToString("N");
            BuildJobKind.RegisterPendingForTest(jobId);
            var record = MakeRecord(jobId);

            Assert.IsTrue(_kind.Cancel(record));
            // Idempotent: already removed from the Pending set, so a second cancel finds nothing.
            Assert.IsFalse(_kind.Cancel(record));
        }

        [Test]
        public void Cancel_UnknownJob_ReturnsFalse()
        {
            Assert.IsFalse(_kind.Cancel(MakeRecord(Guid.NewGuid().ToString("N"))));
        }
    }
}
