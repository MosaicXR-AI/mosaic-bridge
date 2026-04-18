using System;
using System.Collections.Generic;

namespace Mosaic.Bridge.Core.Server
{
    /// <summary>
    /// Per-client token bucket rate limiter. Default: 100 requests/second.
    /// Thread-safe. Keyed by client ID.
    /// </summary>
    public sealed class RateLimiter
    {
        private readonly int _maxTokens;
        private readonly double _refillRate; // tokens per millisecond
        private readonly object _lock = new object();
        private readonly Dictionary<string, TokenBucket> _buckets = new Dictionary<string, TokenBucket>();

        public RateLimiter(int maxRequestsPerSecond = 100)
        {
            _maxTokens = maxRequestsPerSecond;
            _refillRate = maxRequestsPerSecond / 1000.0;
        }

        /// <summary>
        /// Returns true if the request is allowed, false if rate-limited.
        /// </summary>
        public bool TryConsume(string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) clientId = "default";

            lock (_lock)
            {
                if (!_buckets.TryGetValue(clientId, out var bucket))
                {
                    bucket = new TokenBucket(_maxTokens, _refillRate);
                    _buckets[clientId] = bucket;
                }
                return bucket.TryConsume();
            }
        }

        /// <summary>Cleans up stale buckets (not used in 60 seconds).</summary>
        public void CleanupStale()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            lock (_lock)
            {
                var stale = new List<string>();
                foreach (var kvp in _buckets)
                {
                    if (now - kvp.Value.LastAccessMs > 60_000)
                        stale.Add(kvp.Key);
                }
                foreach (var key in stale)
                    _buckets.Remove(key);
            }
        }

        private sealed class TokenBucket
        {
            private double _tokens;
            private long _lastRefillMs;
            private readonly int _max;
            private readonly double _refillRate;

            public long LastAccessMs { get; private set; }

            public TokenBucket(int max, double refillRate)
            {
                _max = max;
                _refillRate = refillRate;
                _tokens = max;
                _lastRefillMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                LastAccessMs = _lastRefillMs;
            }

            public bool TryConsume()
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                LastAccessMs = now;

                // Refill tokens
                var elapsed = now - _lastRefillMs;
                _tokens = Math.Min(_max, _tokens + elapsed * _refillRate);
                _lastRefillMs = now;

                if (_tokens >= 1.0)
                {
                    _tokens -= 1.0;
                    return true;
                }
                return false;
            }
        }
    }
}
