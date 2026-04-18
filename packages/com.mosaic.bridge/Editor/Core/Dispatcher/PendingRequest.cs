using System;
using System.Threading;
using System.Threading.Tasks;
using Mosaic.Bridge.Core.Server;

namespace Mosaic.Bridge.Core.Dispatcher
{
    public sealed class PendingRequest
    {
        public HandlerRequest Request { get; }
        public TaskCompletionSource<HandlerResponse> Tcs { get; }
        public long EnqueuedUnixMs { get; }
        public bool IsReadOnly { get; set; }
        public string ClientId { get; set; }

        /// <summary>
        /// Carries the client's CancellationToken so the main-thread dispatcher
        /// can propagate it into <see cref="ToolExecutionContext"/> during execution.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        public PendingRequest(HandlerRequest request)
        {
            Request = request;
            Tcs = new TaskCompletionSource<HandlerResponse>();
            EnqueuedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
