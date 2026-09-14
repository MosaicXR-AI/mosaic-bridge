using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tests.Unit.Core.Jobs
{
    // L15 / O4 §3.4: JobRegistry is the shared home for any long-running Editor operation that
    // used to be tracked ad hoc (EditorPrefs, SessionState, a static dictionary lost on reload).
    // These tests exercise it against a fake IJobKind so they do not depend on any real Unity
    // async API (package installs, asset generation, ...) actually running.
    [TestFixture]
    public class JobRegistryTests
    {
        private const string Kind = "test/fake-job";

        private class FakeJobKind : IJobKind
        {
            public bool ThrowOnStart;
            public bool CancelAccepted;
            public JobStatus StatusToReportOnProbe = JobStatus.Done;
            public int ProbeCallCount;

            public void Start(JobRecord record, JObject parameters)
            {
                if (ThrowOnStart) throw new System.InvalidOperationException("boom");
                record.Message = "started";
            }

            public void Probe(JobRecord record)
            {
                ProbeCallCount++;
                record.Status = StatusToReportOnProbe;
                if (StatusToReportOnProbe == JobStatus.Done) record.ResultJson = "{\"ok\":true}";
            }

            public bool Cancel(JobRecord record) => CancelAccepted;
        }

        [SetUp]
        public void SetUp()
        {
            JobRegistry.RegisterKind(Kind, new FakeJobKind());
        }

        [Test]
        public void HasKind_AfterRegisterKind_IsTrue()
        {
            Assert.IsTrue(JobRegistry.HasKind(Kind));
        }

        [Test]
        public void HasKind_UnregisteredKind_IsFalse()
        {
            Assert.IsFalse(JobRegistry.HasKind("test/never-registered"));
        }

        [Test]
        public void Start_UnregisteredKind_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => JobRegistry.Start("test/never-registered", new JObject()));
        }

        [Test]
        public void Start_RegisteredKind_ReturnsRunningRecordWithNonEmptyJobId()
        {
            var record = JobRegistry.Start(Kind, new JObject { ["x"] = 1 });

            Assert.IsNotEmpty(record.JobId);
            Assert.AreEqual(Kind, record.Kind);
            Assert.AreEqual(JobStatus.Running, record.Status);
            Assert.AreEqual("started", record.Message);
        }

        [Test]
        public void Start_HandlerThrows_RecordIsFailedNotRunning()
        {
            JobRegistry.RegisterKind(Kind, new FakeJobKind { ThrowOnStart = true });

            var record = JobRegistry.Start(Kind, new JObject());

            Assert.AreEqual(JobStatus.Failed, record.Status);
            StringAssert.Contains("boom", record.Message);
        }

        [Test]
        public void Probe_UnknownJobId_ReturnsNull()
        {
            Assert.IsNull(JobRegistry.Probe("does-not-exist"));
        }

        [Test]
        public void Probe_StillRunning_CallsHandlerProbeAndPersistsStatus()
        {
            var fake = new FakeJobKind { StatusToReportOnProbe = JobStatus.Running };
            JobRegistry.RegisterKind(Kind, fake);
            var started = JobRegistry.Start(Kind, new JObject());

            var probed = JobRegistry.Probe(started.JobId);

            Assert.AreEqual(1, fake.ProbeCallCount);
            Assert.AreEqual(JobStatus.Running, probed.Status);
        }

        [Test]
        public void Probe_HandlerReportsDone_RecordCarriesResultJson()
        {
            var started = JobRegistry.Start(Kind, new JObject());

            var probed = JobRegistry.Probe(started.JobId);

            Assert.AreEqual(JobStatus.Done, probed.Status);
            Assert.AreEqual("{\"ok\":true}", probed.ResultJson);
        }

        [Test]
        public void Probe_AlreadyTerminal_DoesNotCallHandlerAgain()
        {
            var fake = new FakeJobKind();
            JobRegistry.RegisterKind(Kind, fake);
            var started = JobRegistry.Start(Kind, new JObject());
            JobRegistry.Probe(started.JobId); // -> Done
            var callsAfterFirstProbe = fake.ProbeCallCount;

            JobRegistry.Probe(started.JobId);

            Assert.AreEqual(callsAfterFirstProbe, fake.ProbeCallCount,
                "a terminal record has nothing left to re-derive; probing it again must be a no-op");
        }

        [Test]
        public void ListAll_ContainsAStartedJob()
        {
            var started = JobRegistry.Start(Kind, new JObject());

            var all = JobRegistry.ListAll();

            Assert.IsTrue(all.Any(r => r.JobId == started.JobId));
        }

        [Test]
        public void Cancel_HandlerAccepts_MarksCancelled()
        {
            var fake = new FakeJobKind { CancelAccepted = true };
            JobRegistry.RegisterKind(Kind, fake);
            var started = JobRegistry.Start(Kind, new JObject());

            var accepted = JobRegistry.Cancel(started.JobId);

            Assert.IsTrue(accepted);
            var record = JobRegistry.Probe(started.JobId);
            Assert.AreEqual(JobStatus.Cancelled, record.Status);
        }

        [Test]
        public void Cancel_HandlerDeclines_LeavesRecordUnchanged()
        {
            var fake = new FakeJobKind { CancelAccepted = false };
            JobRegistry.RegisterKind(Kind, fake);
            var started = JobRegistry.Start(Kind, new JObject());

            var accepted = JobRegistry.Cancel(started.JobId);

            Assert.IsFalse(accepted);
        }

        [Test]
        public void Cancel_UnknownJobId_ReturnsFalse()
        {
            Assert.IsFalse(JobRegistry.Cancel("does-not-exist"));
        }
    }
}
