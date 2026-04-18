namespace Mosaic.Bridge.Core.Pipeline
{
    /// <summary>
    /// Controls which pipeline stages run for a tool call.
    /// Selected per-call via the execution_mode field in the request body,
    /// with fallback to the user's EditorPrefs default.
    /// </summary>
    public enum ExecutionMode
    {
        /// <summary>Fire-and-forget. No pre/post stages. Zero overhead.</summary>
        Direct = 0,

        /// <summary>Pre-validation + KB suggestions. Typically +5-20ms.</summary>
        Validated = 1,

        /// <summary>Validated + screenshot capture for visual tools. Typically +50-200ms.</summary>
        Verified = 2,

        /// <summary>Verified + structured review context for LLM self-review. Typically +2-10s.</summary>
        Reviewed = 3
    }
}
