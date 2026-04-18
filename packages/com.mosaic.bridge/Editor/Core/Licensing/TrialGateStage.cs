using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Pipeline;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Core.Licensing
{
    /// <summary>
    /// Pipeline pre-stage that enforces trial limits before tool execution.
    /// Blocks requests when the trial has expired or the daily quota is exhausted.
    /// Direct-mode calls (internal/test) bypass this gate.
    /// </summary>
    public sealed class TrialGateStage : IPipelineStage
    {
        private readonly TrialManager _trial;

        public TrialGateStage(TrialManager trial)
        {
            _trial = trial;
        }

        public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
        {
            // Direct mode skips trial check (internal/test calls)
            if (context.Mode == ExecutionMode.Direct)
                return true;

            if (!_trial.RecordToolCall())
            {
                var reason = _trial.GetBlockReason();
                var errorCode = reason == BlockReason.TrialExpired
                    ? ErrorCodes.TRIAL_EXPIRED
                    : ErrorCodes.RATE_LIMITED;
                var message = reason == BlockReason.TrialExpired
                    ? "Your 14-day trial has expired. Visit mosaicxr.com/pricing to upgrade."
                    : $"Daily tool call limit reached ({_trial.DailyQuota}/{_trial.DailyQuota}). Resets at midnight local time.";

                toolResult = new HandlerResponse
                {
                    StatusCode = 403,
                    ContentType = "application/json",
                    Body = JsonConvert.SerializeObject(new
                    {
                        error = errorCode,
                        message,
                        suggestedFix = "Visit https://mosaicxr.com/pricing to upgrade."
                    })
                };
                return false;
            }
            return true;
        }
    }
}
