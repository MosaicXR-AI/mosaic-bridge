using System.Collections.Generic;

namespace Mosaic.Bridge.Core.Authentication
{
    /// <summary>
    /// Thread-safe replay cache for HMAC nonces. Uses two rotating 60-second time buckets so
    /// that a nonce presented near an epoch boundary is still recognised as a replay, while
    /// keeping memory bounded.
    /// </summary>
    /// <remarks>
    /// Fail-closed: when the active bucket reaches its capacity, all subsequent nonces in
    /// the same epoch are rejected. This trades availability for safety — under attack we
    /// would rather drop legitimate traffic than admit a replay because the cache evicted
    /// an entry. There is no eviction policy and no background timer; bucket rotation is
    /// driven entirely by the timestamp passed to <see cref="TryConsume"/>.
    /// </remarks>
    public sealed class NonceCache
    {
        private const int BucketWindowSeconds = 60;

        private readonly object _lock = new object();
        private readonly int _bucketCapacity;

        private HashSet<string> _currentBucket;
        private HashSet<string> _previousBucket;
        private long _currentEpoch;
        private long _previousEpoch;
        private bool _hasPrevious;

        public NonceCache(int bucketCapacity = 30_000)
        {
            _bucketCapacity = bucketCapacity;
            _currentBucket = new HashSet<string>();
            _previousBucket = new HashSet<string>();
            _currentEpoch = long.MinValue;
            _previousEpoch = long.MinValue;
            _hasPrevious = false;
        }

        /// <summary>
        /// Number of distinct nonces currently held in the active bucket. Exposed for
        /// monitoring; not used by the auth path.
        /// </summary>
        public int CurrentCount
        {
            get
            {
                lock (_lock)
                {
                    return _currentBucket.Count;
                }
            }
        }

        /// <summary>
        /// Attempts to register a nonce. Returns <c>true</c> if this is the first time the
        /// nonce has been seen within the active or previous bucket; returns <c>false</c>
        /// if it is a replay or if the active bucket is full.
        /// </summary>
        public bool TryConsume(string nonce, long unixTimestampSeconds)
        {
            if (string.IsNullOrEmpty(nonce))
            {
                return false;
            }

            var epoch = unixTimestampSeconds / BucketWindowSeconds;

            lock (_lock)
            {
                RotateIfNeeded(epoch);

                // Reject if seen in either window — covers the boundary case.
                if (_currentBucket.Contains(nonce))
                {
                    return false;
                }
                if (_hasPrevious && _previousBucket.Contains(nonce))
                {
                    return false;
                }

                // Fail-closed when the active bucket is saturated.
                if (_currentBucket.Count >= _bucketCapacity)
                {
                    return false;
                }

                _currentBucket.Add(nonce);
                return true;
            }
        }

        private void RotateIfNeeded(long epoch)
        {
            // First call ever — bind the current epoch.
            if (_currentEpoch == long.MinValue)
            {
                _currentEpoch = epoch;
                return;
            }

            if (epoch == _currentEpoch)
            {
                return;
            }

            if (epoch == _currentEpoch + 1)
            {
                // Advance one window: previous := current, current := empty.
                _previousBucket = _currentBucket;
                _previousEpoch = _currentEpoch;
                _hasPrevious = true;
                _currentBucket = new HashSet<string>();
                _currentEpoch = epoch;
                return;
            }

            if (epoch > _currentEpoch + 1)
            {
                // Skipped at least one full window — both buckets are stale.
                _previousBucket = new HashSet<string>();
                _previousEpoch = long.MinValue;
                _hasPrevious = false;
                _currentBucket = new HashSet<string>();
                _currentEpoch = epoch;
                return;
            }

            // epoch < _currentEpoch — request from the past.
            // If it lands in the previous window, the existing buckets still apply.
            // Otherwise it is outside the replay window entirely; treat both buckets as
            // unchanged and let the auth-layer clock-skew check reject the request.
        }
    }
}
