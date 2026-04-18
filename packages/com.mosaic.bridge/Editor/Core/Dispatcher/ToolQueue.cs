using System.Collections.Generic;
using Mosaic.Bridge.Core.Server;

namespace Mosaic.Bridge.Core.Dispatcher
{
    public enum BackpressurePolicy { RejectOnFull }

    public enum EnqueueResult { Accepted, RejectedFull, RejectedThreshold }

    public sealed class ToolQueue
    {
        private readonly Dictionary<string, Queue<PendingRequest>> _clientQueues;
        private readonly List<string> _clientOrder;
        private readonly object _lock = new object();
        private int _nextClientIndex;
        private int _totalCount;

        public int Capacity { get; }
        public int WriteRejectThreshold { get; }
        private readonly BackpressurePolicy _policy;

        public ToolQueue(int capacity = 200, BackpressurePolicy policy = BackpressurePolicy.RejectOnFull)
        {
            Capacity = capacity;
            WriteRejectThreshold = (int)(capacity * 0.8);
            _policy = policy;
            _clientQueues = new Dictionary<string, Queue<PendingRequest>>();
            _clientOrder = new List<string>();
            _nextClientIndex = 0;
            _totalCount = 0;
        }

        public int Count
        {
            get { lock (_lock) { return _totalCount; } }
        }

        /// <summary>
        /// Backward-compatible overload — treats as read (always accepted until 100%).
        /// </summary>
        public bool TryEnqueue(PendingRequest request)
        {
            return TryEnqueueClassified(request, isWrite: false) == EnqueueResult.Accepted;
        }

        public EnqueueResult TryEnqueueClassified(PendingRequest request, bool isWrite)
        {
            lock (_lock)
            {
                if (_policy == BackpressurePolicy.RejectOnFull)
                {
                    if (_totalCount >= Capacity)
                        return EnqueueResult.RejectedFull;

                    if (isWrite && _totalCount >= WriteRejectThreshold)
                        return EnqueueResult.RejectedThreshold;
                }

                var clientId = request.ClientId ?? "default";
                if (!_clientQueues.TryGetValue(clientId, out var queue))
                {
                    queue = new Queue<PendingRequest>();
                    _clientQueues[clientId] = queue;
                    _clientOrder.Add(clientId);
                }

                queue.Enqueue(request);
                _totalCount++;
                return EnqueueResult.Accepted;
            }
        }

        /// <summary>
        /// Round-robin dequeue across clients.
        /// </summary>
        public bool TryDequeue(out PendingRequest request)
        {
            lock (_lock)
            {
                if (_totalCount == 0)
                {
                    request = null;
                    return false;
                }

                for (int attempts = 0; attempts < _clientOrder.Count; attempts++)
                {
                    int idx = (_nextClientIndex + attempts) % _clientOrder.Count;
                    var clientId = _clientOrder[idx];
                    var queue = _clientQueues[clientId];

                    if (queue.Count > 0)
                    {
                        request = queue.Dequeue();
                        _totalCount--;
                        _nextClientIndex = (idx + 1) % _clientOrder.Count;

                        // Clean up empty client queues
                        if (queue.Count == 0)
                        {
                            _clientQueues.Remove(clientId);
                            _clientOrder.RemoveAt(idx);
                            // After removing at idx, items shift down.
                            // The next client is now at position idx in the shortened list.
                            if (_clientOrder.Count > 0)
                                _nextClientIndex = idx % _clientOrder.Count;
                            else
                                _nextClientIndex = 0;
                        }

                        return true;
                    }
                }

                request = null;
                return false;
            }
        }

        /// <summary>
        /// Drains ALL items across all client queues, completing each with the given response.
        /// Used during domain reload to unblock waiting HTTP threads.
        /// </summary>
        public int DrainWith(HandlerResponse response)
        {
            int count = 0;
            lock (_lock)
            {
                foreach (var kvp in _clientQueues)
                {
                    while (kvp.Value.Count > 0)
                    {
                        var pr = kvp.Value.Dequeue();
                        pr.Tcs.TrySetResult(response);
                        count++;
                    }
                }
                _clientQueues.Clear();
                _clientOrder.Clear();
                _nextClientIndex = 0;
                _totalCount = 0;
            }
            return count;
        }
    }
}
