using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Packages
{
    /// <summary>Same fix as PackageAddJobKind (L15), for the identical blocking pattern in
    /// package/remove.</summary>
    internal sealed class PackageRemoveJobKind : IJobKind
    {
        private static readonly Dictionary<string, RemoveRequest> Live = new Dictionary<string, RemoveRequest>();

        public void Start(JobRecord record, JObject parameters)
        {
            var name = (string)parameters["Name"];
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required");
            Live[record.JobId] = Client.Remove(name);
            record.Message = $"Removing '{name}'…";
        }

        public void Probe(JobRecord record)
        {
            if (Live.TryGetValue(record.JobId, out var request))
            {
                if (!request.IsCompleted)
                {
                    record.Status = JobStatus.Running;
                    return;
                }
                Live.Remove(record.JobId);
                if (request.Status == StatusCode.Failure)
                {
                    record.Status = JobStatus.Failed;
                    record.Message = request.Error?.message ?? "Unknown error";
                    return;
                }
                record.Status = JobStatus.Done;
                record.Message = $"Removed {request.PackageIdOrName}";
                return;
            }

            // Reload took the live request with it. Recover from durable evidence: is the
            // package actually gone now?
            var name = ExtractPackageName(record.ParamsJson);
            var stillInstalled = !string.IsNullOrEmpty(name) && PackageInfo.FindForPackageName(name) != null;
            if (!stillInstalled)
            {
                record.Status = JobStatus.Done;
                record.Message = $"'{name}' is confirmed no longer installed — a domain reload interrupted the " +
                                  "original request, but the removal did complete.";
                return;
            }

            record.Status = JobStatus.Running;
            record.Message = "A domain reload interrupted the original request; the package still appears " +
                              "installed. Keep polling — if this persists, resubmit package/remove.";
        }

        public bool Cancel(JobRecord record) => false;

        private static string ExtractPackageName(string paramsJson)
        {
            try { return (string)JObject.Parse(paramsJson ?? "{}")["Name"]; }
            catch { return null; }
        }
    }
}
