using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.EditorOps
{
    // ── Submit ────────────────────────────────────────────────────────────────

    /// <remarks>
    /// H-2: the generated block schedules its own execution via
    /// <c>EditorApplication.delayCall</c>, which fires on a future Editor tick. An unfocused
    /// Editor window does not tick on its own — see the identical problem, root-caused and
    /// solved, on <c>editor/play-mode</c> (<c>EditorPlayModeTool</c>'s <c>PumpSeconds</c>/
    /// <c>Pump</c>). Confirmed on Windows: a submitted block that never ran, ran immediately
    /// once the Editor window was given OS-level focus (SetForegroundWindow), with zero code
    /// changes in between. Without a fix, the tool's own advice — "resubmit once the Editor is
    /// idle" — asks a bridge-driven caller to fix a Windows quirk it cannot control.
    ///
    /// The fix reuses play-mode's exact mechanism: hook <c>EditorApplication.update</c> and call
    /// <c>QueuePlayerLoopUpdate()</c> on every tick to force the Editor to keep advancing while a
    /// job is pending, regardless of focus. It differs from play-mode in one way that matters:
    /// this pump must survive the domain reload that follows compilation, because that reload is
    /// exactly when the generated class registers its delayCall. A plain field-based pump does
    /// not survive a reload (static state resets). So the pump here is re-armed by
    /// <c>[InitializeOnLoad]</c> on EVERY load — including the one right after the reload — by
    /// checking a small EditorPrefs-persisted list of still-pending job ids.
    /// </remarks>
    [InitializeOnLoad]
    public static class EditorRunBlockTool
    {
        private const string TempFolderParent = "Assets";
        private const string TempFolderName   = "Editor";
        private const string TempFolder       = "Assets/Editor";
        private const string ClassPrefix      = "MosaicBridge_RunBlock_";
        private const string PrefPrefix       = "MosaicBridgeRunBlock_";
        private const string ActiveJobsKey    = PrefPrefix + "ActiveJobs";

        /// <summary>
        /// After this many seconds with no result and no active compilation, a poll gives up and
        /// reports a timeout (see <see cref="EditorRunBlockPollTool"/>). Also the pump's own
        /// budget: there is no point driving Editor ticks past the point a caller has stopped
        /// waiting for them.
        /// </summary>
        internal const int PendingTimeoutSeconds = 20;

        /// <summary>
        /// H-2 regression (beta.23, live): a job whose compile+reload genuinely takes longer
        /// than <see cref="PendingTimeoutSeconds"/> — a large project's assembly, this bridge's
        /// own ~300+ tools, and the education/Pro packages all recompiling together — was
        /// indistinguishable from an abandoned one at the 20-second mark, and THREE separate
        /// places treated "not done at 20s, no compile errors" as "gone for good": Poll deleted
        /// the script and reported a terminal error even though the block had compiled and was
        /// simply slow to run; RearmPumpForPendingJobs dropped the job from the active list on
        /// the very reload it most needed the pump for; and the orphan sweep (N-1) then deleted
        /// a script whose class had not had its chance to execute yet. Deleting mid-flight is
        /// what turned "slow" into "provably can never run": removing the .cs forces another
        /// compile, which is another domain reload, which discards the delayCall the generated
        /// class's OWN static constructor had already registered.
        ///
        /// This is the actual line between "still plausibly compiling" and "abandoned." It is
        /// deliberately far larger than PendingTimeoutSeconds — that constant governs polling
        /// cadence messages, not deletion — and the three sites below all defer to it before
        /// destroying anything.
        /// </summary>
        internal const int MinOrphanAgeSeconds = 120;

        /// <summary>
        /// O-2 (beta.24, field report): raising the patience window to
        /// <see cref="MinOrphanAgeSeconds"/> fixed the false "abandoned" verdict, but Poll's
        /// "keep waiting" branch had no ceiling at all — a job that compiled cleanly and then
        /// genuinely never reached <c>_done</c> (observed: a larger block that hung with
        /// <c>compile-status</c> reporting <c>Settled: true</c>, zero errors, for 70+ seconds
        /// and counting) waited forever, exactly like O-1's poll-gated cleanup: no terminal
        /// state, so the script was never released either. This is the actual "genuinely
        /// stuck" ceiling; MinOrphanAgeSeconds only ever meant "not abandoned yet."
        /// </summary>
        internal const int HardTimeoutSeconds = 300;

        /// <summary>
        /// O-1 (field report): the orphan sweep ran once per domain reload — fine for a session
        /// with ongoing activity, but a job that finishes successfully and is simply never
        /// polled has no reason to trigger another reload on its own, so its script could sit
        /// in the project indefinitely in an otherwise-idle Editor. This is the interval a
        /// second, independent hook re-runs the same sweep on, so cleanup no longer depends on
        /// something else happening to cause a reload.
        /// </summary>
        internal const double PeriodicSweepIntervalSeconds = 60.0;

        private static double _pumpUntil;
        private static bool _hooked;
        private static double _nextPeriodicSweepAt;

        /// <summary>Test hook: whether the pump is currently subscribed to EditorApplication.update.</summary>
        internal static bool IsPumping => _hooked;

        /// <summary>Test hook: forces the next PeriodicSweepTick to actually run, rather than a
        /// test's pass/fail depending on how long this Editor session has been open.</summary>
        internal static void ForcePeriodicSweepDueForTests() => _nextPeriodicSweepAt = 0;

        static EditorRunBlockTool()
        {
            RearmPumpForPendingJobs();

            // Deferred, not called straight from here: this constructor runs during the domain
            // reload, and AssetDatabase mutations at that point are the documented way to get an
            // import into an inconsistent state. The sweep is housekeeping — nothing waits on it,
            // so the next tick is soon enough.
            EditorApplication.delayCall += () => SweepOrphanedTempScripts();

            // O-1: a permanent, lightweight hook so the sweep also runs on a timer, not only on
            // the next domain reload — which may never come in an Editor that is otherwise idle
            // after a job's temp script stopped being polled. Unlike the pump above, this one is
            // never unsubscribed; per-tick cost is one double comparison until the interval is due.
            _nextPeriodicSweepAt = EditorApplication.timeSinceStartup + PeriodicSweepIntervalSeconds;
            EditorApplication.update += PeriodicSweepTick;
        }

        /// <summary>Internal for the O-1 regression test — exercises the same code path
        /// EditorApplication.update drives, without waiting real time for the interval.</summary>
        internal static void PeriodicSweepTick()
        {
            if (EditorApplication.timeSinceStartup < _nextPeriodicSweepAt) return;
            _nextPeriodicSweepAt = EditorApplication.timeSinceStartup + PeriodicSweepIntervalSeconds;
            SweepOrphanedTempScripts();
        }

        [MosaicTool("editor/run-block",
                    "Submits a multi-statement C# code block for execution inside the Unity Editor. " +
                    "Use this for custom logic that has NO dedicated MCP tool: " +
                    "data queries, bulk renames, custom AssetDatabase operations, editor automation, etc. " +
                    "⛔ DO NOT use this tool for anything that has a dedicated MCP tool — it is slower, " +
                    "   harder to debug, and bypasses workflow enforcement. Specifically, NEVER use " +
                    "   editor/run-block to: create GameObjects (use gameobject/create or scene/create-object), " +
                    "   create ProBuilder meshes (use probuilder/create), create materials (use material/create), " +
                    "   instantiate prefabs (use asset/instantiate_prefab), write shaders (use shadergraph/*), " +
                    "   or build any 3D object from ProBuilder API calls. " +
                    "HOW IT WORKS: " +
                    "1. The block is wrapped in a temp [InitializeOnLoad] Editor class and compiled by Unity. " +
                    "2. After compilation (3-10 seconds), the code runs automatically on the next Editor tick. " +
                    "3. Call editor/run-block-poll with the returned JobId to check the result. " +
                    "NOTE: A domain reload occurs during compilation — this is normal. " +
                    "NOTE: Do NOT call this if the project already has compile errors. " +
                    "Default usings included: System, System.Collections.Generic, System.Linq, " +
                    "UnityEngine, UnityEditor. Add extras via Usings param if needed. " +
                    "Debug.Log() calls inside the block are captured and returned in Output.",
                    isReadOnly: false)]
        public static ToolResult<RunBlockSubmitResult> Submit(RunBlockParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Code))
                return ToolResult<RunBlockSubmitResult>.Fail(
                    "Code is required.", ErrorCodes.INVALID_PARAM);

            // Ensure Assets/Editor exists
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder(TempFolderParent, TempFolderName);

            string jobId     = Guid.NewGuid().ToString("N").Substring(0, 12);
            string className = ClassPrefix + jobId;
            string scriptPath = TempFolder + "/" + className + ".cs";
            string fullPath   = Path.GetFullPath(scriptPath);

            // Clear any stale prefs from a previous job with the same ID (astronomically unlikely)
            ClearJobPrefs(jobId);

            // Write the temp script
            string script = BuildScript(className, jobId, p.Code, p.Usings);
            File.WriteAllText(fullPath, script);

            // Record submission time BEFORE triggering compile (survives domain reload)
            long submitted = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            EditorPrefs.SetString(PrefPrefix + jobId + "_scriptPath",  scriptPath);
            EditorPrefs.SetString(PrefPrefix + jobId + "_submitted",   submitted.ToString());
            AddActiveJobId(jobId);

            // Trigger compilation
            AssetDatabase.ImportAsset(scriptPath, ImportAssetOptions.ForceSynchronousImport);

            // H-2: keep driving Editor ticks until the job finishes or times out, so the
            // generated class's delayCall fires even if this window never gets focus. Re-armed
            // by the static constructor after the domain reload the compile above triggers.
            StartPump(PendingTimeoutSeconds);

            return ToolResult<RunBlockSubmitResult>.Ok(new RunBlockSubmitResult
            {
                JobId   = jobId,
                Status  = "pending",
                Message = "Script submitted. Unity is compiling (expect 3-10 seconds + domain reload). " +
                          "Call editor/run-block-poll with this JobId to get the result."
            });
        }

        // ── Pump (H-2) ────────────────────────────────────────────────────────

        /// <summary>
        /// Re-arms the pump for every job that was submitted but hadn't finished before this
        /// load — including the load immediately after the domain reload triggered by
        /// compiling the temp script, which is exactly when the generated class registers its
        /// delayCall. Drops jobs from the active list once they're done or have exceeded
        /// <see cref="MinOrphanAgeSeconds"/> — a large project's compile can comfortably outlast
        /// <see cref="PendingTimeoutSeconds"/> without being abandoned, and pruning it here stops
        /// the very pump it still needs.
        /// </summary>
        internal static void RearmPumpForPendingJobs()
        {
            var ids = GetActiveJobIds();
            if (ids.Count == 0) return;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var stillPending = new List<string>();
            foreach (var id in ids)
            {
                if (EditorPrefs.GetBool(PrefPrefix + id + "_done", false))
                    continue; // finished — drop it

                var submittedStr = EditorPrefs.GetString(PrefPrefix + id + "_submitted", "0");
                long submitted = long.TryParse(submittedStr, out long ts) ? ts : 0;
                // NOT PendingTimeoutSeconds: that is a polling-message threshold, and a job
                // whose compile is merely slow crosses it while genuinely still in flight — the
                // regression this method caused by pruning (and so un-pumping) exactly here.
                if (now - submitted >= MinOrphanAgeSeconds)
                    continue; // actually abandoned — editor/run-block-poll's own floor agrees

                stillPending.Add(id);
            }

            SetActiveJobIds(stillPending);
            if (stillPending.Count > 0)
                StartPump(PendingTimeoutSeconds);
        }

        // ── Orphan sweep (N-1) ────────────────────────────────────────────────

        /// <summary>
        /// Deletes generated run-block scripts in <see cref="TempFolder"/> that no live job owns.
        /// Returns how many were removed.
        /// </summary>
        /// <remarks>
        /// Cleanup used to happen in exactly one place: <c>editor/run-block-poll</c>, once a job
        /// reached a terminal state. A caller who submitted a block and never polled it to
        /// completion — or gave up while it still said "compiling" — left its script in the
        /// project permanently. That is why a field session found six stranded at once with
        /// successes and failures alike among them: the predictor was never the job's outcome, it
        /// was whether anyone polled it to the end.
        ///
        /// They do not sit there inertly. Each is an <c>[InitializeOnLoad]</c> class, so every one
        /// recompiles and runs on every subsequent domain reload, and they are counted as project
        /// scripts by the course tooling — one report had the build gate reading 13 scripts in a
        /// project that had 7. They would also ship inside a course project handed to learners.
        ///
        /// Runs on every load, AFTER <see cref="RearmPumpForPendingJobs"/> has pruned the
        /// active-job list. A file is only ever removed once it is BOTH absent from that list
        /// AND older than <see cref="MinOrphanAgeSeconds"/> — the list alone was proven
        /// insufficient (a live regression: a slow compile fell out of it while the job was
        /// still genuinely in flight, and deleting its script on that basis alone guaranteed it
        /// could never run). Age is the independent check that survives a bookkeeping mistake.
        /// </remarks>
        internal static int SweepOrphanedTempScripts()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) return 0;

            string[] files;
            try
            {
                files = Directory.GetFiles(Path.GetFullPath(TempFolder),
                                           ClassPrefix + "*.cs", SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                return 0; // an unreadable folder is not worth failing a domain reload over
            }
            if (files.Length == 0) return 0;

            var live = new HashSet<string>(GetActiveJobIds());
            int removed = 0;
            foreach (var full in files)
            {
                string name  = Path.GetFileNameWithoutExtension(full);
                string jobId = name.Length > ClassPrefix.Length
                    ? name.Substring(ClassPrefix.Length)
                    : "";
                if (jobId.Length == 0 || live.Contains(jobId)) continue;

                // "Not in the active list" is not, by itself, proof of abandonment — that list
                // is exactly what a slow compile can fall out of (see MinOrphanAgeSeconds). The
                // file's own age is the independent signal: nothing this bridge generates is
                // still mid-flight two minutes after its last write.
                double ageSeconds;
                try { ageSeconds = (DateTime.UtcNow - File.GetLastWriteTimeUtc(full)).TotalSeconds; }
                catch (Exception) { continue; } // unreadable timestamp: leave it, next load retries
                if (ageSeconds < MinOrphanAgeSeconds) continue;

                if (DeleteScriptFile(TempFolder + "/" + Path.GetFileName(full)))
                    removed++;
                ClearJobPrefs(jobId);
            }

            if (removed > 0)
                UnityEngine.Debug.Log($"[Mosaic.Bridge] Removed {removed} orphaned editor/run-block " +
                                      $"temp script(s) from {TempFolder}.");
            return removed;
        }

        /// <summary>Deletes a generated script and its .meta — through the AssetDatabase where
        /// that works, directly where it does not. True if the script is gone afterwards.</summary>
        /// <remarks>
        /// <c>AssetDatabase.DeleteAsset</c> RETURNS false rather than throwing when it cannot
        /// delete, which it does during compilation among other times. The old cleanup only
        /// guarded against exceptions, so a returned false read as success — a script reported
        /// cleaned up and still sitting in the project. That is the second half of N-1, and it is
        /// the half that makes the failure silent.
        /// </remarks>
        internal static bool DeleteScriptFile(string assetPath)
        {
            try
            {
                if (AssetDatabase.DeleteAsset(assetPath)) return true;
            }
            catch (Exception)
            {
                // fall through — the direct delete below is the point of this method
            }

            try
            {
                string full = Path.GetFullPath(assetPath);
                bool gone = false;
                if (File.Exists(full)) { File.Delete(full); gone = true; }
                if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
                return gone || !File.Exists(full);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Same mechanism as EditorPlayModeTool's PumpSeconds: subscribe to
        /// EditorApplication.update and force a player-loop tick on every callback, which is
        /// what actually drives the Editor forward — including delayCall dispatch — when the
        /// window has no focus.</summary>
        internal static void StartPump(double timeoutSeconds)
        {
            var until = EditorApplication.timeSinceStartup + timeoutSeconds;
            if (until > _pumpUntil) _pumpUntil = until;
            if (_hooked) return;
            EditorApplication.update += Pump;
            _hooked = true;
        }

        internal static void StopPump()
        {
            _pumpUntil = 0;
            if (!_hooked) return;
            EditorApplication.update -= Pump;
            _hooked = false;
        }

        private static void Pump()
        {
            if (EditorApplication.timeSinceStartup >= _pumpUntil || GetActiveJobIds().Count == 0)
            {
                StopPump();
                return;
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }

        // ── Active-job bookkeeping ────────────────────────────────────────────
        // EditorPrefs exposes no key-enumeration API, so the set of pending job ids has to be
        // tracked explicitly under one fixed key to be discoverable again after a domain reload.

        internal static void AddActiveJobId(string jobId)
        {
            var ids = GetActiveJobIds();
            if (!ids.Contains(jobId)) ids.Add(jobId);
            SetActiveJobIds(ids);
        }

        internal static void RemoveActiveJobId(string jobId)
        {
            var ids = GetActiveJobIds();
            if (ids.Remove(jobId))
                SetActiveJobIds(ids);
        }

        internal static List<string> GetActiveJobIds()
        {
            var raw = EditorPrefs.GetString(ActiveJobsKey, "");
            var list = new List<string>();
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (var id in raw.Split(','))
                if (!string.IsNullOrEmpty(id)) list.Add(id);
            return list;
        }

        private static void SetActiveJobIds(List<string> ids)
        {
            if (ids.Count == 0)
                EditorPrefs.DeleteKey(ActiveJobsKey);
            else
                EditorPrefs.SetString(ActiveJobsKey, string.Join(",", ids));
        }

        // ── Script builder ────────────────────────────────────────────────────

        private static string BuildScript(string className, string jobId, string code, string[] extraUsings)
        {
            // Pre-compute EditorPrefs keys as string literals (avoids const expression issues)
            string doneKey   = PrefPrefix + jobId + "_done";
            string statusKey = PrefPrefix + jobId + "_status";
            string outputKey = PrefPrefix + jobId + "_output";
            string errorKey  = PrefPrefix + jobId + "_error";

            var sb = new StringBuilder();

            // ── Default usings ────────────────────────────────────────────────
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEditor;");

            if (extraUsings != null)
            {
                foreach (string raw in extraUsings)
                {
                    string ns = raw.Trim();
                    // Strip leading "using " and trailing ";"
                    if (ns.StartsWith("using ", StringComparison.Ordinal)) ns = ns.Substring(6).Trim();
                    ns = ns.TrimEnd(';').Trim();
                    if (!string.IsNullOrEmpty(ns))
                        sb.AppendLine("using " + ns + ";");
                }
            }

            sb.AppendLine();

            // ── Class shell ───────────────────────────────────────────────────
            sb.AppendLine("[InitializeOnLoad]");
            sb.AppendLine("public static class " + className);
            sb.AppendLine("{");

            // Static constructor: only schedule once
            sb.AppendLine("    static " + className + "()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (UnityEditor.EditorPrefs.GetBool(\"" + doneKey + "\", false)) return;");
            sb.AppendLine("        UnityEditor.EditorApplication.delayCall += _RunOnce;");
            sb.AppendLine("    }");
            sb.AppendLine();

            // _RunOnce: captures Debug.Log, runs user code, writes result to EditorPrefs
            sb.AppendLine("    private static void _RunOnce()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (UnityEditor.EditorPrefs.GetBool(\"" + doneKey + "\", false)) return;");
            sb.AppendLine("        UnityEditor.EditorPrefs.SetBool(\"" + doneKey + "\", true);");
            sb.AppendLine();
            sb.AppendLine("        var _logs = new System.Collections.Generic.List<string>();");
            sb.AppendLine("        UnityEngine.Application.LogCallback _handler =");
            sb.AppendLine("            (msg, trace, type) => _logs.Add(\"[\" + type + \"] \" + msg);");
            sb.AppendLine("        UnityEngine.Application.logMessageReceived += _handler;");
            sb.AppendLine();
            sb.AppendLine("        try");
            sb.AppendLine("        {");

            // ── User code (indented) ──────────────────────────────────────────
            foreach (string line in code.Split('\n'))
                sb.AppendLine("            " + line.TrimEnd('\r'));

            sb.AppendLine();
            sb.AppendLine("            UnityEngine.Application.logMessageReceived -= _handler;");
            sb.AppendLine("            UnityEditor.EditorPrefs.SetString(\"" + statusKey + "\", \"done\");");
            sb.AppendLine("            UnityEditor.EditorPrefs.SetString(\"" + outputKey + "\",");
            sb.AppendLine("                string.Join(\"\\n\", _logs));");
            sb.AppendLine("        }");
            sb.AppendLine("        catch (System.Exception _ex)");
            sb.AppendLine("        {");
            sb.AppendLine("            UnityEngine.Application.logMessageReceived -= _handler;");
            sb.AppendLine("            UnityEditor.EditorPrefs.SetString(\"" + statusKey + "\", \"error\");");
            sb.AppendLine("            UnityEditor.EditorPrefs.SetString(\"" + errorKey + "\",");
            sb.AppendLine("                _ex.GetType().Name + \": \" + _ex.Message);");
            sb.AppendLine("            UnityEditor.EditorPrefs.SetString(\"" + outputKey + "\",");
            sb.AppendLine("                string.Join(\"\\n\", _logs));");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        // ── EditorPrefs cleanup ───────────────────────────────────────────────

        internal static void ClearJobPrefs(string jobId)
        {
            string p = PrefPrefix + jobId;
            EditorPrefs.DeleteKey(p + "_done");
            EditorPrefs.DeleteKey(p + "_status");
            EditorPrefs.DeleteKey(p + "_output");
            EditorPrefs.DeleteKey(p + "_error");
            EditorPrefs.DeleteKey(p + "_scriptPath");
            EditorPrefs.DeleteKey(p + "_submitted");
            RemoveActiveJobId(jobId);
        }
    }

    // ── Poll ──────────────────────────────────────────────────────────────────

    public static class EditorRunBlockPollTool
    {
        private const string PrefPrefix = "MosaicBridgeRunBlock_";

        // After this many seconds with no result and no active compilation → assume compile
        // error. Shared with EditorRunBlockTool's own pump budget (H-2) so the two agree on how
        // long a job is worth still driving Editor ticks for.
        private const int CompileErrorTimeoutSeconds = EditorRunBlockTool.PendingTimeoutSeconds;

        [MosaicTool("editor/run-block-poll",
                    "Polls for the result of a previously submitted editor/run-block job. " +
                    "Pass the JobId returned by editor/run-block. " +
                    "Status: 'compiling' — still compiling, wait 3s and retry. " +
                    "Status: 'pending' — compilation done but code not yet executed, retry in 1s. " +
                    "Status: 'done' — code ran successfully; Output contains any Debug.Log lines. " +
                    "Status: 'error' — runtime exception (see Error) or compile failure (check console/get-errors). " +
                    "Cleans up the temp script automatically once result is read (causes one more domain reload).",
                    isReadOnly: true)]
        public static ToolResult<RunBlockPollResult> Poll(RunBlockPollParams p)
        {
            if (string.IsNullOrWhiteSpace(p.JobId))
                return ToolResult<RunBlockPollResult>.Fail(
                    "JobId is required.", ErrorCodes.INVALID_PARAM);

            string jobId = p.JobId.Trim();
            string prefBase = PrefPrefix + jobId;

            bool isDone = EditorPrefs.GetBool(prefBase + "_done", false);

            if (isDone)
            {
                string status = EditorPrefs.GetString(prefBase + "_status", "unknown");
                string output = EditorPrefs.GetString(prefBase + "_output", "");
                string error  = EditorPrefs.GetString(prefBase + "_error",  "");
                string script = EditorPrefs.GetString(prefBase + "_scriptPath", "");

                DeleteTempScript(jobId, script);

                return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
                {
                    JobId   = jobId,
                    Status  = status,
                    Output  = string.IsNullOrEmpty(output) ? null : output,
                    Error   = string.IsNullOrEmpty(error)  ? null : error,
                    Message = status == "done"
                        ? "Code executed successfully."
                        : "Execution failed: " + error
                });
            }

            // ── Not done yet ─────────────────────────────────────────────────

            if (EditorApplication.isCompiling)
                return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
                {
                    JobId   = jobId,
                    Status  = "compiling",
                    Message = "Unity is still compiling. Try again in 3 seconds."
                });

            // Compilation finished but _done not set yet.
            // Either: (a) domain-reload just happened, delayCall hasn't fired yet
            //      or (b) compile errors prevented [InitializeOnLoad] from running.
            // Distinguish using submission timestamp.
            string submittedStr = EditorPrefs.GetString(prefBase + "_submitted", "0");
            long submitted = long.TryParse(submittedStr, out long ts) ? ts : 0;
            long elapsed   = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - submitted;

            if (elapsed < CompileErrorTimeoutSeconds)
                return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
                {
                    JobId   = jobId,
                    Status  = "pending",
                    Message = "Compilation done, code execution pending (domain reload just completed). " +
                              "Try again in 2 seconds."
                });

            // Real compile errors mean the job is genuinely dead — a broken script in
            // Assets/Editor never runs, and it also keeps the whole project from compiling, so it
            // has to go now regardless of how little time has passed. Harvest the errors BEFORE
            // deleting: sending the caller to console/get-errors for a file the bridge has already
            // removed is two round trips to learn something this call already had in hand.
            string scriptPath = EditorPrefs.GetString(prefBase + "_scriptPath", "");
            string compileErrors = CollectCompileErrors(jobId);
            bool didNotCompile = !string.IsNullOrEmpty(compileErrors);

            if (didNotCompile)
            {
                DeleteTempScript(jobId, scriptPath);
                return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
                {
                    JobId   = jobId,
                    Status  = "error",
                    Error   = compileErrors,
                    Message = $"Job timed out after {elapsed}s — the script did not compile. The errors are "
                        + "in Error, below. Fix the code and resubmit. NOTE: the temp script has been "
                        + "deleted, so the file the console names no longer exists — the line numbers "
                        + "refer to the block you submitted, offset by the generated header."
                });
            }

            // No compile errors: the script compiled and is simply slow to run, which is exactly
            // what a large project's assembly (this bridge's own ~300+ tools, education/Pro, and
            // the customer's own scripts, all recompiling together) produces past the 20-second
            // mark this branch used to give up at. That mark was wrong: it was the confirmed cause
            // of a live regression where a job that would have completed a few seconds later was
            // deleted here instead, on every submission, because deleting it forces ANOTHER
            // compile — another domain reload — which discards the delayCall the generated class's
            // own static constructor had already registered, before it ever got to fire. Compile
            // errors would already have been caught above, so patience here has nothing to lose.
            if (elapsed < EditorRunBlockTool.MinOrphanAgeSeconds)
                return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
                {
                    JobId   = jobId,
                    Status  = "pending",
                    Message = $"Compilation done ({elapsed}s ago), execution still pending. No compile "
                        + "errors — a large project's assembly can take longer than usual to finish "
                        + "reloading. Try again in 3 seconds. Do NOT resubmit; a second job racing the "
                        + "first is the one thing that can still make this fail."
                });

            // Past MinOrphanAgeSeconds but under the hard ceiling: still no compile errors, so
            // this is not the beta.23 regression — genuinely just slow. Keep waiting, but say so
            // more plainly now that "a moment longer" has become "a while".
            if (elapsed < EditorRunBlockTool.HardTimeoutSeconds)
            {
                // O-2 (field report): this tier was reached once with a job that never went on
                // to finish, and nothing recorded that it happened — it was visible only to
                // whoever was watching the poll responses at the time. One log line per job (not
                // per poll, which would spam every 5 seconds) gives the next report the exact
                // job id and elapsed time regardless of who was watching.
                var warnedKey = PrefPrefix + jobId + "_warnedUnusual";
                if (!EditorPrefs.GetBool(warnedKey, false))
                {
                    EditorPrefs.SetBool(warnedKey, true);
                    UnityEngine.Debug.LogWarning(
                        $"[Mosaic.Bridge] editor/run-block job {jobId} has been executing for {elapsed}s with " +
                        "no compile errors — past the point a slow compile explains (see O-2). Still polling; " +
                        "logged here so a hang that outlasts the caller's patience is not only visible to " +
                        "whoever happened to be watching.");
                }
                return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
                {
                    JobId   = jobId,
                    Status  = "pending",
                    Message = $"Still executing after {elapsed}s with no compile errors. Unusual, but not "
                        + "yet abandoned — keep polling every 5 seconds. Do NOT resubmit."
                });
            }

            // O-2: past even the hard ceiling with no result and no compile errors, the job is
            // genuinely stuck — most likely an infinite loop or a blocking wait in the submitted
            // code — and will never call home on its own. Report it AND release the script here,
            // rather than leaving it to a caller that may never poll again: O-1's other half was
            // exactly this, a job with no terminal state whose temp script sat in the project
            // indefinitely because nothing but a poll ever cleaned one up.
            DeleteTempScript(jobId, scriptPath);
            return ToolResult<RunBlockPollResult>.Ok(new RunBlockPollResult
            {
                JobId   = jobId,
                Status  = "error",
                Error   = null,
                Message = $"Job timed out after {elapsed}s with no result and no compile errors — well "
                    + "past the time any compile, however large the project, should ever take. "
                    + "The likeliest cause is the submitted code itself hanging (an infinite loop, a "
                    + "blocking wait). Resubmit something that returns."
            });
        }

        /// <summary>
        /// Compile errors from this job's generated script, newest first, or "" if none were found.
        /// </summary>
        /// <remarks>
        /// Filtered to the job's own file so an unrelated pre-existing error in the project is not
        /// reported as the cause of this failure — the tool already warns not to submit a block
        /// into a project that does not compile, and blaming a stale error would make that warning
        /// harder to act on rather than easier.
        /// </remarks>
        private static string CollectCompileErrors(string jobId)
        {
            try
            {
                var entries = Mosaic.Bridge.Tools.ConsoleTools.ConsoleLogBuffer.GetEntries(
                    includeInfo: false, includeWarnings: false, includeErrors: true, maxResults: 50);
                var sb = new StringBuilder();
                foreach (var e in entries)
                {
                    string where = e.File ?? "";
                    string what  = e.Message ?? "";
                    if (where.IndexOf(jobId, StringComparison.Ordinal) < 0 &&
                        what.IndexOf(jobId, StringComparison.Ordinal) < 0)
                        continue;
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(what.Trim());
                }
                return sb.ToString();
            }
            catch (Exception)
            {
                // Never let diagnostics turn a compile failure into a tool failure.
                return "";
            }
        }

        private static void DeleteTempScript(string jobId, string scriptPath)
        {
            EditorRunBlockTool.ClearJobPrefs(jobId);

            if (string.IsNullOrEmpty(scriptPath)) return;

            string full = Path.GetFullPath(scriptPath);
            if (!File.Exists(full)) return;

            // Not best-effort any more: a failure here leaves an [InitializeOnLoad] class in the
            // user's project that recompiles on every reload and is counted as one of their own
            // scripts (N-1). EditorRunBlockTool's sweep catches whatever still slips through on
            // the next load, but the delete itself has to actually try.
            EditorRunBlockTool.DeleteScriptFile(scriptPath);
        }
    }

    // ── Params & Results ──────────────────────────────────────────────────────

    public sealed class RunBlockParams
    {
        [Required] public string   Code   { get; set; }
        public           string[]  Usings { get; set; }
    }

    public sealed class RunBlockPollParams
    {
        [Required] public string JobId { get; set; }
    }

    public sealed class RunBlockSubmitResult
    {
        public string JobId   { get; set; }
        public string Status  { get; set; }
        public string Message { get; set; }
    }

    public sealed class RunBlockPollResult
    {
        public string JobId   { get; set; }
        public string Status  { get; set; }
        public string Output  { get; set; }
        public string Error   { get; set; }
        public string Message { get; set; }
    }
}
