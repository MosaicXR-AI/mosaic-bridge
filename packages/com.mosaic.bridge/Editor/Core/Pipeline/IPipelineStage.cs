using Mosaic.Bridge.Core.Server;

namespace Mosaic.Bridge.Core.Pipeline
{
    /// <summary>
    /// A single stage in the execution pipeline. Stages run before or after tool execution
    /// depending on their registration. A stage can short-circuit the pipeline by returning false.
    /// </summary>
    public interface IPipelineStage
    {
        /// <summary>
        /// Executes this pipeline stage.
        /// </summary>
        /// <param name="context">Shared execution context with tool metadata and mutable state.</param>
        /// <param name="toolResult">
        /// For pre-stages: null (tool hasn't executed yet).
        /// For post-stages: the tool's response (may be inspected but not replaced).
        /// </param>
        /// <returns>
        /// True to continue the pipeline. False to abort — the stage must set
        /// <paramref name="toolResult"/> or add a rejection to <see cref="ExecutionContext.Warnings"/>.
        /// </returns>
        bool Execute(ExecutionContext context, ref HandlerResponse toolResult);
    }
}
