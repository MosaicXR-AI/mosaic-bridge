using System;
using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.EditorOps;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    // H-2: editor/run-block schedules its generated block's execution via
    // EditorApplication.delayCall, which needs an Editor tick to fire. An unfocused Editor
    // window does not tick on its own, so the job silently never ran — proven by resubmitting
    // the byte-identical block after forcing OS-level focus, which ran it immediately.
    //
    // The fix drives EditorApplication.update itself (the same mechanism editor/play-mode
    // already uses for its PumpSeconds) for as long as a job is pending, and re-arms that pump
    // via [InitializeOnLoad] so it survives the domain reload that follows compilation — that
    // reload is exactly when the generated class registers its own delayCall.
    //
    // Submit() itself triggers a real compile + domain reload (AssetDatabase.ImportAsset with
    // ForceSynchronousImport), which is unsafe to do from a Unit test — so these tests exercise
    // the pump and its EditorPrefs-backed bookkeeping directly, without going through Submit().
    [TestFixture]
    public class EditorRunBlockToolTests
    {
        private const string TestJobId = "unittest0001";

        [TearDown]
        public void TearDown()
        {
            // Static pump state and EditorPrefs both persist across tests/runs — reset both so
            // one test's fixture can't leak into the next.
            EditorRunBlockTool.StopPump();
            EditorRunBlockTool.ClearJobPrefs(TestJobId);
        }

        // ── Active-job bookkeeping ───────────────────────────────────────────

        [Test]
        public void AddActiveJobId_ThenGet_ContainsIt()
        {
            EditorRunBlockTool.AddActiveJobId(TestJobId);

            CollectionAssert.Contains(EditorRunBlockTool.GetActiveJobIds(), TestJobId);
        }

        [Test]
        public void AddActiveJobId_CalledTwice_DoesNotDuplicate()
        {
            EditorRunBlockTool.AddActiveJobId(TestJobId);
            EditorRunBlockTool.AddActiveJobId(TestJobId);

            var ids = EditorRunBlockTool.GetActiveJobIds();
            Assert.AreEqual(1, ids.FindAll(id => id == TestJobId).Count);
        }

        [Test]
        public void RemoveActiveJobId_RemovesIt()
        {
            EditorRunBlockTool.AddActiveJobId(TestJobId);
            EditorRunBlockTool.RemoveActiveJobId(TestJobId);

            CollectionAssert.DoesNotContain(EditorRunBlockTool.GetActiveJobIds(), TestJobId);
        }

        [Test]
        public void ClearJobPrefs_AlsoRemovesFromActiveJobs()
        {
            // ClearJobPrefs is called both when Submit() reuses a stale id and when Poll() has
            // read a finished job's result — either way, the job must stop being tracked as
            // pending, or the pump would keep driving ticks for a job nobody is waiting on.
            EditorRunBlockTool.AddActiveJobId(TestJobId);

            EditorRunBlockTool.ClearJobPrefs(TestJobId);

            CollectionAssert.DoesNotContain(EditorRunBlockTool.GetActiveJobIds(), TestJobId);
        }

        // ── Pump re-arming (the actual H-2 fix) ──────────────────────────────

        [Test]
        public void RearmPump_JobStillPendingAndNotTimedOut_StartsPumpingAndKeepsJobActive()
        {
            EditorPrefs.SetString("MosaicBridgeRunBlock_" + TestJobId + "_submitted",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            EditorRunBlockTool.AddActiveJobId(TestJobId);

            EditorRunBlockTool.RearmPumpForPendingJobs();

            Assert.IsTrue(EditorRunBlockTool.IsPumping,
                "a job that is neither done nor timed out must keep the pump driving Editor ticks");
            CollectionAssert.Contains(EditorRunBlockTool.GetActiveJobIds(), TestJobId);
        }

        [Test]
        public void RearmPump_JobAlreadyDone_DropsItAndDoesNotPump()
        {
            EditorPrefs.SetBool("MosaicBridgeRunBlock_" + TestJobId + "_done", true);
            EditorPrefs.SetString("MosaicBridgeRunBlock_" + TestJobId + "_submitted",
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            EditorRunBlockTool.AddActiveJobId(TestJobId);

            EditorRunBlockTool.RearmPumpForPendingJobs();

            CollectionAssert.DoesNotContain(EditorRunBlockTool.GetActiveJobIds(), TestJobId,
                "a finished job has nothing left to pump for and must be dropped");
            Assert.IsFalse(EditorRunBlockTool.IsPumping);

            EditorPrefs.DeleteKey("MosaicBridgeRunBlock_" + TestJobId + "_done");
        }

        [Test]
        public void RearmPump_JobTimedOut_DropsItAndDoesNotPump()
        {
            var longAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (EditorRunBlockTool.PendingTimeoutSeconds + 5);
            EditorPrefs.SetString("MosaicBridgeRunBlock_" + TestJobId + "_submitted", longAgo.ToString());
            EditorRunBlockTool.AddActiveJobId(TestJobId);

            EditorRunBlockTool.RearmPumpForPendingJobs();

            CollectionAssert.DoesNotContain(EditorRunBlockTool.GetActiveJobIds(), TestJobId,
                "a job past the timeout is editor/run-block-poll's problem to report, not the pump's to keep driving");
            Assert.IsFalse(EditorRunBlockTool.IsPumping);
        }

        [Test]
        public void RearmPump_NoActiveJobs_IsANoOpAndDoesNotStartPumping()
        {
            EditorRunBlockTool.RearmPumpForPendingJobs();

            Assert.IsFalse(EditorRunBlockTool.IsPumping);
        }

        [Test]
        public void StartPump_ThenStopPump_UnhooksFromEditorUpdate()
        {
            EditorRunBlockTool.StartPump(5);
            Assert.IsTrue(EditorRunBlockTool.IsPumping);

            EditorRunBlockTool.StopPump();
            Assert.IsFalse(EditorRunBlockTool.IsPumping);
        }
    }
}
