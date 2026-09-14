using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Mosaic.Bridge.Core.Jobs
{
    /// <summary>
    /// O4 §3.4: today there are three different, mutually inconsistent answers to "does this
    /// job outlive the request" (EditorPrefs for run-block, SessionState for test/run, static
    /// dictionaries — lost on reload — for Pro's generation jobs), and one tool (package/add,
    /// L15) that just blocks the main thread with Thread.Sleep for up to 30 seconds and can lose
    /// its own response entirely if a domain reload lands mid-wait. This is the one registry
    /// every long-running Editor operation should sit on instead.
    /// </summary>
    /// <remarks>
    /// Records persist in SessionState — survives a domain reload, dies with the Editor, exactly
    /// the lifetime a job actually has. Live request/task objects a job kind holds onto are
    /// static and EXPECTED to die in a reload; MarkRunningAsInterrupted marks every job still
    /// Pending/Running as Interrupted the moment a reload is about to happen, so a caller polling
    /// afterward sees an honest "we don't know yet, ask the job kind's own Probe to check durable
    /// evidence" rather than a job that silently vanishes.
    /// </remarks>
    [InitializeOnLoad]
    public static class JobRegistry
    {
        private const string IndexKey = "MosaicBridge.Jobs.Index";
        private const string RecordKeyPrefix = "MosaicBridge.Jobs.Record.";
        private const string ProgressKeyPrefix = "MosaicBridge.Jobs.Progress.";

        private static readonly Dictionary<string, IJobKind> Kinds = new Dictionary<string, IJobKind>();

        static JobRegistry()
        {
            AssemblyReloadEvents.beforeAssemblyReload += MarkRunningAsInterrupted;
        }

        /// <summary>Registers (or replaces) the handler for a job kind, e.g. "package/add".</summary>
        public static void RegisterKind(string kind, IJobKind handler) => Kinds[kind] = handler;

        public static bool HasKind(string kind) => Kinds.ContainsKey(kind);

        public static JobRecord Start(string kind, JObject parameters)
        {
            if (!Kinds.TryGetValue(kind, out var handler))
                throw new InvalidOperationException($"No job kind registered with JobRegistry: '{kind}'");

            var record = new JobRecord
            {
                JobId = Guid.NewGuid().ToString("N").Substring(0, 12),
                Kind = kind,
                StartedUtc = DateTime.UtcNow.ToString("o"),
                ParamsJson = parameters?.ToString(Formatting.None) ?? "{}",
                Status = JobStatus.Pending,
            };
            AddToIndex(record.JobId);

            var progressId = UnityEditor.Progress.Start(
                $"Mosaic: {kind}", null, UnityEditor.Progress.Options.Unmanaged | UnityEditor.Progress.Options.Indefinite);
            SessionState.SetInt(ProgressKeyPrefix + record.JobId, progressId);

            try
            {
                handler.Start(record, parameters);
                record.Status = JobStatus.Running;
            }
            catch (Exception e)
            {
                record.Status = JobStatus.Failed;
                record.Message = $"Failed to start: {e.Message}";
                FinishProgress(record.JobId, JobStatus.Failed);
            }
            Save(record);
            return record;
        }

        /// <summary>Re-derives status from the job kind's own Probe when still in flight, then
        /// returns the (possibly updated) record. Null if JobId is unknown.</summary>
        public static JobRecord Probe(string jobId)
        {
            var record = Load(jobId);
            if (record == null) return null;
            if (record.Status == JobStatus.Pending || record.Status == JobStatus.Running)
            {
                if (Kinds.TryGetValue(record.Kind, out var handler))
                {
                    try
                    {
                        handler.Probe(record);
                    }
                    catch (Exception e)
                    {
                        record.Status = JobStatus.Failed;
                        record.Message = $"Probe threw: {e.Message}";
                    }
                }
                else
                {
                    record.Status = JobStatus.Failed;
                    record.Message = $"No job kind registered for '{record.Kind}' in this Editor session " +
                                      "(likely started before a reload that removed it).";
                }
                Save(record);
                if (IsTerminal(record.Status)) FinishProgress(jobId, record.Status);
            }
            return record;
        }

        public static List<JobRecord> ListAll() =>
            ReadIndex().Select(Load).Where(r => r != null).ToList();

        public static bool Cancel(string jobId)
        {
            var record = Load(jobId);
            if (record == null || !Kinds.TryGetValue(record.Kind, out var handler)) return false;
            if (!handler.Cancel(record)) return false;
            record.Status = JobStatus.Cancelled;
            Save(record);
            FinishProgress(jobId, JobStatus.Cancelled);
            return true;
        }

        private static bool IsTerminal(JobStatus s) =>
            s == JobStatus.Done || s == JobStatus.Failed || s == JobStatus.Cancelled || s == JobStatus.Interrupted;

        private static void MarkRunningAsInterrupted()
        {
            foreach (var record in ListAll())
            {
                if (record.Status != JobStatus.Pending && record.Status != JobStatus.Running) continue;
                record.Status = JobStatus.Interrupted;
                record.Message = "A domain reload interrupted this job before it produced a result. " +
                                  "Call job/status again — some job kinds can still recover the real " +
                                  "outcome from durable evidence once the reload finishes.";
                Save(record);
            }
        }

        private static void FinishProgress(string jobId, JobStatus status)
        {
            var key = ProgressKeyPrefix + jobId;
            var id = SessionState.GetInt(key, -1);
            SessionState.EraseInt(key);
            if (id < 0 || !UnityEditor.Progress.Exists(id)) return;
            var pStatus = status == JobStatus.Done ? UnityEditor.Progress.Status.Succeeded
                : status == JobStatus.Cancelled ? UnityEditor.Progress.Status.Canceled
                : UnityEditor.Progress.Status.Failed;
            UnityEditor.Progress.Finish(id, pStatus);
        }

        private static void AddToIndex(string jobId)
        {
            var ids = ReadIndex();
            if (!ids.Contains(jobId)) ids.Add(jobId);
            SessionState.SetString(IndexKey, string.Join(",", ids));
        }

        private static List<string> ReadIndex()
        {
            var raw = SessionState.GetString(IndexKey, "");
            return string.IsNullOrEmpty(raw) ? new List<string>() : raw.Split(',').Where(s => s.Length > 0).ToList();
        }

        private static void Save(JobRecord record) =>
            SessionState.SetString(RecordKeyPrefix + record.JobId, JsonConvert.SerializeObject(record));

        private static JobRecord Load(string jobId)
        {
            var raw = SessionState.GetString(RecordKeyPrefix + jobId, "");
            if (string.IsNullOrEmpty(raw)) return null;
            try { return JsonConvert.DeserializeObject<JobRecord>(raw); }
            catch { return null; }
        }
    }
}
