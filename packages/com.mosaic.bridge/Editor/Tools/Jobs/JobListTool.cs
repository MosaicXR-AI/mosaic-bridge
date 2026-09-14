using System.Linq;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Jobs
{
    public static class JobListTool
    {
        [MosaicTool("job/list",
                    "Lists every job this Editor session has started through the job registry (package/add and " +
                    "future job kinds), optionally filtered by Kind. Does not include Unity's own native " +
                    "background tasks (asset import, shader compile) — those never go through this registry.",
                    isReadOnly: true, category: "job")]
        public static ToolResult<JobListResult> Execute(JobListParams p)
        {
            var jobs = JobRegistry.ListAll()
                .Where(r => string.IsNullOrEmpty(p?.Kind) || r.Kind == p.Kind)
                .Select(r => new JobStatusResult
                {
                    JobId = r.JobId,
                    Kind = r.Kind,
                    Status = r.Status.ToString().ToLowerInvariant(),
                    Message = r.Message,
                    ResultJson = r.ResultJson,
                })
                .ToList();

            return ToolResult<JobListResult>.Ok(new JobListResult { Jobs = jobs });
        }
    }
}
