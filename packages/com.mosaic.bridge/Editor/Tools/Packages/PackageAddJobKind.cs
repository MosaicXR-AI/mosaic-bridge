using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Packages
{
    /// <summary>
    /// L15 fix: package/add used to Thread.Sleep-loop on the main thread for up to 30 seconds,
    /// and a domain reload landing mid-wait (which a package install commonly causes) could lose
    /// the response entirely. This kind starts the request and returns immediately; job/status
    /// does the waiting, one poll at a time, off the thread that would otherwise be blocked.
    /// </summary>
    internal sealed class PackageAddJobKind : IJobKind
    {
        // Keyed by JobId. Expected to be empty again after a domain reload — Probe falls back to
        // PackageInfo.FindForPackageName at that point, per O4 §3.4's own inventory of this exact
        // operation ("after reload probe PackageInfo.FindForPackageName").
        private static readonly Dictionary<string, AddRequest> Live = new Dictionary<string, AddRequest>();

        public void Start(JobRecord record, JObject parameters)
        {
            var identifier = (string)parameters["Identifier"];
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("Identifier is required");
            Live[record.JobId] = Client.Add(identifier);
            record.Message = $"Installing '{identifier}'…";
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
                record.Message = $"Installed {request.Result.displayName} ({request.Result.name}@{request.Result.version})";
                record.ResultJson = JsonConvert.SerializeObject(PackageListTool.ToPackageRef(request.Result));
                return;
            }

            // The live AddRequest is gone — almost certainly a domain reload, exactly the case
            // that used to lose the response outright. Recover from durable evidence: is the
            // package actually installed now?
            var name = ExtractPackageName(record.ParamsJson);
            var info = string.IsNullOrEmpty(name) ? null : PackageInfo.FindForPackageName(name);
            if (info != null)
            {
                record.Status = JobStatus.Done;
                record.Message = $"Installed {info.displayName} ({info.name}@{info.version}) — confirmed after " +
                                  "a domain reload interrupted the original request.";
                record.ResultJson = JsonConvert.SerializeObject(PackageListTool.ToPackageRef(info));
                return;
            }

            // Genuinely unknown: the request object is gone and the package is not installed.
            // Could still be resolving after the reload — leave it Running rather than guess
            // Failed, since a false failure sends the caller retrying an install already in
            // flight.
            record.Status = JobStatus.Running;
            record.Message = "A domain reload interrupted the original request; the package is not yet visible " +
                              "as installed. Keep polling — if this persists, resubmit package/add.";
        }

        public bool Cancel(JobRecord record) => false; // Unity's Client.Add offers no cancellation

        private static string ExtractPackageName(string paramsJson)
        {
            try
            {
                var identifier = (string)JObject.Parse(paramsJson ?? "{}")["Identifier"];
                if (string.IsNullOrEmpty(identifier)) return null;
                // Strip "@version" and a git URL's own shape does not resolve by name anyway —
                // FindForPackageName only ever matches a registry package id.
                var at = identifier.IndexOf('@');
                return at > 0 ? identifier.Substring(0, at) : identifier;
            }
            catch
            {
                return null;
            }
        }
    }
}
