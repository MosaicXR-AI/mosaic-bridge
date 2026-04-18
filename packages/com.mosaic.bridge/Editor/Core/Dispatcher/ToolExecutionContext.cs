using System.Threading;

namespace Mosaic.Bridge.Core.Dispatcher
{
    /// <summary>
    /// Provides ambient access to the CancellationToken for the currently executing tool.
    /// Set by MainThreadDispatcher/ExecutionPipeline before invoking a tool, cleared afterwards.
    /// Tools can opt-in to cancellation by checking <see cref="CancellationToken"/> at key points.
    /// </summary>
    public static class ToolExecutionContext
    {
        private static readonly AsyncLocal<CancellationToken> _token = new AsyncLocal<CancellationToken>();

        /// <summary>
        /// The CancellationToken for the current tool execution.
        /// Returns <see cref="CancellationToken.None"/> if no token has been set.
        /// </summary>
        public static CancellationToken CancellationToken => _token.Value;

        /// <summary>Sets the token for the current execution scope. Called by the dispatcher.</summary>
        internal static void Set(CancellationToken ct) => _token.Value = ct;

        /// <summary>Clears the token after execution completes. Called by the dispatcher.</summary>
        internal static void Clear() => _token.Value = default;
    }
}
