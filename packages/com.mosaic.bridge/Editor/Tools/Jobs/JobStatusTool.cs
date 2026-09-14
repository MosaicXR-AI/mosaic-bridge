using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Jobs
{
    /// <summary>
    /// The one poll route for every job started through JobRegistry (package/add today; more
    /// job kinds migrate onto the same registry over time — see the O4 job registry plan).
    /// </summary>
    public static class JobStatusTool
    {
        [MosaicTool("job/status",
                    "Checks an asynchronous job's status by the JobId a starting tool returned (e.g. package/add). " +
                    "Status is one of pending, running, done, failed, cancelled, interrupted. Poll every few " +
                    "seconds until it is no longer pending or running.",
                    isReadOnly: true, category: "job")]
        public static ToolResult<JobStatusResult> Execute(JobStatusParams p)
        {
            if (string.IsNullOrEmpty(p?.JobId))
                return ToolResult<JobStatusResult>.Fail("JobId is required", ErrorCodes.INVALID_PARAM);

            var record = JobRegistry.Probe(p.JobId);
            if (record == null)
                return ToolResult<JobStatusResult>.Fail(
                    $"Unknown JobId '{p.JobId}'. It may be from a previous Editor session.", ErrorCodes.NOT_FOUND);

            return ToolResult<JobStatusResult>.Ok(new JobStatusResult
            {
                JobId = record.JobId,
                Kind = record.Kind,
                Status = record.Status.ToString().ToLowerInvariant(),
                Message = record.Message,
                ResultJson = record.ResultJson,
            });
        }
    }
}
