using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Build
{
    /// <summary>
    /// O4 §3.4: `BuildPipeline.BuildPlayer` is fully synchronous — it blocks the Editor main
    /// thread, the same thread the bridge's dispatcher runs on, for however long the build takes.
    /// build/build calling it directly is the "45-minute hang" (backlog 2026-08-26): the HTTP
    /// request for build/build itself sits open for the build's entire duration and can time out
    /// on the caller's side despite the build succeeding.
    ///
    /// This kind's Start does NOT call BuildPipeline.BuildPlayer — it schedules the call via
    /// EditorApplication.delayCall (the same pattern the O4 table gives Addressables' build:
    /// "schedule via delayCall so the HTTP response goes first") and returns immediately, so
    /// build/start's own HTTP response is never the thing blocked.
    ///
    /// This does NOT make the build non-blocking — nothing can, short of the ThreadAffinity=Any
    /// dispatcher change O4 §3.4 defers ("Until then build/start returns {JobId, "Editor will be
    /// unresponsive"}"). A job/status poll sent WHILE the scheduled build is actually running will
    /// itself wait for the main thread, same as any other tool call would. What is fixed is the
    /// start call: it returns in milliseconds instead of tens of minutes.
    /// </summary>
    internal sealed class BuildJobKind : IJobKind
    {
        private static readonly HashSet<string> Pending = new HashSet<string>();
        private static readonly Dictionary<string, BuildReport> Reports = new Dictionary<string, BuildReport>();

        public void Start(JobRecord record, JObject parameters)
        {
            var p = new BuildParams
            {
                Target = (string)parameters["Target"] ?? "current",
                OutputPath = (string)parameters["OutputPath"],
                Development = (bool?)parameters["Development"] ?? false,
                AutoRunPlayer = (bool?)parameters["AutoRunPlayer"] ?? false,
                ShowBuiltPlayer = (bool?)parameters["ShowBuiltPlayer"] ?? false,
            };

            if (!BuildToolHelpers.TryResolveTarget(p.Target, out var buildTarget, out var targetError))
                throw new ArgumentException(targetError);
            if (!BuildToolHelpers.TryBuildOptions(p, buildTarget, out var options, out var optionsError))
                throw new ArgumentException(optionsError);

            Pending.Add(record.JobId);
            var jobId = record.JobId;
            EditorApplication.delayCall += () => RunBuild(jobId, options, buildTarget);

            record.Message = "Editor will be unresponsive while the player build runs. Poll job/status — a " +
                              "call made while the build is actually in progress will itself wait for the " +
                              "main thread, same as any tool call would.";
        }

        private static void RunBuild(string jobId, BuildPlayerOptions options, BuildTarget target)
        {
            if (!Pending.Remove(jobId)) return; // cancelled before it ran
            var report = BuildPipeline.BuildPlayer(options);
            Reports[jobId] = report;
            // Stash the target alongside the report — BuildTarget is a struct on BuildReport's own
            // summary (BuildTargetGroup/platform), but ToResult wants the exact enum we resolved,
            // not a re-derivation, so it travels with the report via a side table.
            ReportTargets[jobId] = target;
        }

        private static readonly Dictionary<string, BuildTarget> ReportTargets = new Dictionary<string, BuildTarget>();

        public void Probe(JobRecord record)
        {
            if (Reports.TryGetValue(record.JobId, out var report))
            {
                Reports.Remove(record.JobId);
                ReportTargets.TryGetValue(record.JobId, out var target);
                ReportTargets.Remove(record.JobId);

                record.Status = report.summary.result == BuildResult.Succeeded ? JobStatus.Done : JobStatus.Failed;
                var result = BuildToolHelpers.ToResult(report, target, 0);
                record.ResultJson = JsonConvert.SerializeObject(result);
                record.Message = record.Status == JobStatus.Done
                    ? $"Build succeeded — {result.OutputPath}"
                    : $"Build failed — {result.Errors.Length} error(s), see ResultJson.";
                return;
            }

            if (Pending.Contains(record.JobId) || BuildPipeline.isBuildingPlayer)
            {
                record.Status = JobStatus.Running;
                return;
            }

            // Neither pending, reported, nor actively building. A domain reload cannot normally
            // preempt a synchronous BuildPipeline.BuildPlayer call — it runs to completion on the
            // one thread a reload would also need — but this is still the honest fallback if the
            // in-memory dictionaries above were ever lost while a build was genuinely in flight:
            // Library/LastBuild.buildreport's own write time is durable evidence a build finished
            // after this job started.
            var reportPath = Path.Combine("Library", "LastBuild.buildreport");
            if (File.Exists(reportPath) &&
                DateTime.TryParse(record.StartedUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var startedUtc) &&
                File.GetLastWriteTimeUtc(reportPath) >= startedUtc)
            {
                record.Status = JobStatus.Done;
                record.Message = "Build finished (confirmed via Library/LastBuild.buildreport after a domain " +
                                  "reload interrupted this Editor's live handle on it). Re-run build/start for " +
                                  "full error/warning detail — that could not be recovered from this evidence " +
                                  "alone.";
                return;
            }

            record.Status = JobStatus.Running;
            record.Message = "A domain reload interrupted this Editor's live handle on the build job. Still " +
                              "polling — no evidence yet that it finished.";
        }

        public bool Cancel(JobRecord record) => Pending.Remove(record.JobId); // only before delayCall fires

        /// <summary>Tests only: mark a job pending without going through Start (which needs a
        /// real BuildTarget resolvable in this Editor and schedules an actual player build —
        /// far too slow and disk-heavy for a unit test).</summary>
        internal static void RegisterPendingForTest(string jobId) => Pending.Add(jobId);
    }
}
