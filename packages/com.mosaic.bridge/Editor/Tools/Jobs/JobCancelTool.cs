using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Jobs
{
    public static class JobCancelTool
    {
        [MosaicTool("job/cancel",
                    "Requests cancellation of a running job. Best-effort: most Unity async operations (package " +
                    "add/remove) offer no cancel at all, in which case Cancelled is false and the job keeps running " +
                    "— check job/status for the real outcome.",
                    isReadOnly: false, category: "job")]
        public static ToolResult<JobCancelResult> Execute(JobCancelParams p)
        {
            if (string.IsNullOrEmpty(p?.JobId))
                return ToolResult<JobCancelResult>.Fail("JobId is required", ErrorCodes.INVALID_PARAM);

            var cancelled = JobRegistry.Cancel(p.JobId);
            return ToolResult<JobCancelResult>.Ok(new JobCancelResult
            {
                JobId = p.JobId,
                Cancelled = cancelled,
            });
        }
    }
}
