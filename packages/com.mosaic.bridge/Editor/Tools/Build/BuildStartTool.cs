using Newtonsoft.Json.Linq;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Build
{
    public static class BuildStartTool
    {
        [MosaicTool("build/start",
                    "Starts a player build and returns a JobId immediately — use this instead of build/build, " +
                    "which blocks on the HTTP request for the build's entire duration and can time out the " +
                    "caller despite the build succeeding. Poll job/status with the JobId; Status 'done' or " +
                    "'failed' carries the same result shape as build/build in ResultJson. NOTE: the Editor's " +
                    "main thread is genuinely blocked while the build runs (Unity gives no non-blocking build " +
                    "API) — a job/status call made during that window will itself wait for the build to finish " +
                    "before it can answer, same as any tool call would.",
                    isReadOnly: false, category: "build")]
        public static ToolResult<BuildStartResult> Execute(BuildParams p)
        {
            var parameters = new JObject
            {
                ["Target"] = p.Target,
                ["OutputPath"] = p.OutputPath,
                ["Development"] = p.Development,
                ["AutoRunPlayer"] = p.AutoRunPlayer,
                ["ShowBuiltPlayer"] = p.ShowBuiltPlayer,
            };

            JobRecord record;
            try { record = JobRegistry.Start(BuildJobsBootstrap.Kind, parameters); }
            catch (System.Exception e)
            {
                return ToolResult<BuildStartResult>.Fail(e.Message, ErrorCodes.INVALID_PARAM);
            }

            if (record.Status == JobStatus.Failed)
                return ToolResult<BuildStartResult>.Fail(record.Message, ErrorCodes.INVALID_PARAM);

            return ToolResult<BuildStartResult>.Ok(new BuildStartResult
            {
                JobId = record.JobId,
                Message = record.Message,
            });
        }
    }

    public sealed class BuildStartResult
    {
        public string JobId { get; set; }
        public string Message { get; set; }
    }
}
