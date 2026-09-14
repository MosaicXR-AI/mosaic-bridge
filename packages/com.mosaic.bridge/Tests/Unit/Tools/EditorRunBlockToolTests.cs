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
        public void ThreeTimeoutConstants_EachStrictlyLargerThanTheLast()
        {
            // O-2: HardTimeoutSeconds exists because MinOrphanAgeSeconds alone had no ceiling —
            // a job that compiled cleanly and then genuinely never finished waited forever, with
            // its script never released. If these three ever collapse into each other again, the
            // corresponding "keep waiting" branch in Poll silently stops existing.
            Assert.Less(EditorRunBlockTool.PendingTimeoutSeconds, EditorRunBlockTool.MinOrphanAgeSeconds);
            Assert.Less(EditorRunBlockTool.MinOrphanAgeSeconds, EditorRunBlockTool.HardTimeoutSeconds);
        }

        [Test]
        public void RearmPump_PastPendingTimeoutButUnderOrphanAge_StaysActiveAndPumping()
        {
            // The regression, isolated: a job merely slow to compile (past the 20s messaging
            // threshold, nowhere near actually abandoned) must not be dropped here. Dropping it
            // stopped the pump on the very reload it most needed it, and let the orphan sweep
            // treat "not currently active" as "safe to delete" — deleting a script whose class
            // had not yet had the chance to run.
            var slowButAlive = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (EditorRunBlockTool.PendingTimeoutSeconds + 5);
            EditorPrefs.SetString("MosaicBridgeRunBlock_" + TestJobId + "_submitted", slowButAlive.ToString());
            EditorRunBlockTool.AddActiveJobId(TestJobId);

            EditorRunBlockTool.RearmPumpForPendingJobs();

            CollectionAssert.Contains(EditorRunBlockTool.GetActiveJobIds(), TestJobId,
                "past PendingTimeoutSeconds is not past MinOrphanAgeSeconds — a large project's " +
                "compile can legitimately take longer than the 20s messaging threshold");
            Assert.IsTrue(EditorRunBlockTool.IsPumping,
                "the job still needs ticks driven to ever get its delayCall dispatched");
        }

        [Test]
        public void RearmPump_PastOrphanAge_DropsItAndDoesNotPump()
        {
            var longAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (EditorRunBlockTool.MinOrphanAgeSeconds + 5);
            EditorPrefs.SetString("MosaicBridgeRunBlock_" + TestJobId + "_submitted", longAgo.ToString());
            EditorRunBlockTool.AddActiveJobId(TestJobId);

            EditorRunBlockTool.RearmPumpForPendingJobs();

            CollectionAssert.DoesNotContain(EditorRunBlockTool.GetActiveJobIds(), TestJobId,
                "past MinOrphanAgeSeconds with no result is actually abandoned");
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

        // ── Orphan sweep (N-1) ───────────────────────────────────────────────
        //
        // Cleanup used to run only inside run-block-poll, on a job reaching a terminal state, so
        // a block nobody polled to completion left its generated [InitializeOnLoad] script in the
        // project permanently — six were found stranded in one field session, successes and
        // failures alike, because what predicted it was never the outcome but whether anyone
        // polled. These write the generated files directly rather than through Submit(), which
        // triggers a real compile + domain reload and is unsafe from a Unit test.

        private const string SweepFolder = "Assets/Editor";
        private const string SweepPrefix = "MosaicBridge_RunBlock_";

        private static string WriteFakeTempScript(string jobId)
        {
            var dir = System.IO.Path.GetFullPath(SweepFolder);
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, SweepPrefix + jobId + ".cs");
            System.IO.File.WriteAllText(path, "// placeholder for a run-block temp script\n");
            return path;
        }

        private static void Backdate(string path, double secondsAgo)
        {
            var t = DateTime.UtcNow.AddSeconds(-secondsAgo);
            System.IO.File.SetLastWriteTimeUtc(path, t);
        }

        [Test]
        public void Sweep_DeletesAScriptThatIsBothInactiveAndOld()
        {
            const string orphan = "unittestorph";
            var path = WriteFakeTempScript(orphan);
            Backdate(path, EditorRunBlockTool.MinOrphanAgeSeconds + 5);
            try
            {
                Assert.IsTrue(System.IO.File.Exists(path), "fixture did not write");

                EditorRunBlockTool.SweepOrphanedTempScripts();

                Assert.IsFalse(System.IO.File.Exists(path),
                    "a generated script nobody is tracking, old enough that nothing could still be " +
                    "compiling it, must not survive the load");
            }
            finally
            {
                EditorRunBlockTool.ClearJobPrefs(orphan);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void Sweep_LeavesAScriptWhoseJobIsStillInFlight()
        {
            // Deleting an in-flight job's script would guarantee the job could never run — the
            // generated class is the thing the pending domain reload is waiting to execute.
            var path = WriteFakeTempScript(TestJobId);
            try
            {
                EditorPrefs.SetString("MosaicBridgeRunBlock_" + TestJobId + "_submitted",
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
                EditorRunBlockTool.AddActiveJobId(TestJobId);

                EditorRunBlockTool.SweepOrphanedTempScripts();

                Assert.IsTrue(System.IO.File.Exists(path),
                    "a job still in the active list has not run yet; its script must be left alone");
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void Sweep_LeavesAFreshScriptEvenWhenNotInTheActiveList()
        {
            // The regression, isolated at the sweep's own boundary: a job can fall out of the
            // active list (a slow compile, or any bookkeeping gap) while its script is seconds
            // old and its class has not run yet. "Not active" alone must never be sufficient —
            // age is the independent signal that a bookkeeping mistake elsewhere cannot defeat.
            const string fresh = "unittestfrsh";
            var path = WriteFakeTempScript(fresh);
            try
            {
                EditorRunBlockTool.SweepOrphanedTempScripts();

                Assert.IsTrue(System.IO.File.Exists(path),
                    "a script written moments ago cannot be presumed abandoned merely because " +
                    "it is not (or no longer) in the active-job list");
            }
            finally
            {
                EditorRunBlockTool.ClearJobPrefs(fresh);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void PeriodicSweepTick_AlsoCleansUpAnOldOrphan()
        {
            // O-1: cleanup used to depend entirely on a future domain reload happening at all —
            // which a job that finished successfully and was simply never polled again gives no
            // reason to trigger. PeriodicSweepTick is the independent, timer-driven path that
            // does not need one; this exercises the exact call EditorApplication.update makes.
            const string orphan = "unittestptck";
            var path = WriteFakeTempScript(orphan);
            Backdate(path, EditorRunBlockTool.MinOrphanAgeSeconds + 5);
            try
            {
                Assert.IsTrue(System.IO.File.Exists(path), "fixture did not write");

                EditorRunBlockTool.ForcePeriodicSweepDueForTests();
                EditorRunBlockTool.PeriodicSweepTick();

                Assert.IsFalse(System.IO.File.Exists(path),
                    "the periodic tick must reach the same orphan the reload-triggered sweep would");
            }
            finally
            {
                EditorRunBlockTool.ClearJobPrefs(orphan);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void DeleteScriptFile_RemovesTheFileAndItsMeta()
        {
            const string id = "unittestdel0";
            var path = WriteFakeTempScript(id);
            System.IO.File.WriteAllText(path + ".meta", "fileFormatVersion: 2\n");
            try
            {
                var ok = EditorRunBlockTool.DeleteScriptFile(SweepFolder + "/" + SweepPrefix + id + ".cs");

                Assert.IsTrue(ok, "delete reported failure for a file that was plainly there");
                Assert.IsFalse(System.IO.File.Exists(path));
                Assert.IsFalse(System.IO.File.Exists(path + ".meta"),
                    "a left-behind .meta is how Unity re-creates the asset entry on the next refresh");
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                if (System.IO.File.Exists(path + ".meta")) System.IO.File.Delete(path + ".meta");
            }
        }
    }
}
