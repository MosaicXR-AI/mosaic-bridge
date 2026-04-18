using System.Collections.Generic;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// Thread-safe replay cache for HMAC nonces at runtime.
    /// Uses two rotating 60-second time buckets — identical algorithm to the editor NonceCache.
    /// </summary>
    public sealed class RuntimeNonceCache
    {
        private const int BucketWindowSeconds = 60;

        private readonly object _lock = new object();
        private readonly int _bucketCapacity;

        private HashSet<string> _currentBucket;
        private HashSet<string> _previousBucket;
        private long _currentEpoch;
        private long _previousEpoch;
        private bool _hasPrevious;

        public RuntimeNonceCache(int bucketCapacity = 30_000)
        {
            _bucketCapacity = bucketCapacity;
            _currentBucket = new HashSet<string>();
            _previousBucket = new HashSet<string>();
            _currentEpoch = long.MinValue;
            _previousEpoch = long.MinValue;
            _hasPrevious = false;
        }

        public int CurrentCount
        {
            get { lock (_lock) { return _currentBucket.Count; } }
        }

        public bool TryConsume(string nonce, long unixTimestampSeconds)
        {
            if (string.IsNullOrEmpty(nonce))
                return false;

            var epoch = unixTimestampSeconds / BucketWindowSeconds;

            lock (_lock)
            {
                RotateIfNeeded(epoch);

                if (_currentBucket.Contains(nonce))
                    return false;
                if (_hasPrevious && _previousBucket.Contains(nonce))
                    return false;
                if (_currentBucket.Count >= _bucketCapacity)
                    return false;

                _currentBucket.Add(nonce);
                return true;
            }
        }

        private void RotateIfNeeded(long epoch)
        {
            if (_currentEpoch == long.MinValue)
            {
                _currentEpoch = epoch;
                return;
            }

            if (epoch == _currentEpoch)
                return;

            if (epoch == _currentEpoch + 1)
            {
                _previousBucket = _currentBucket;
                _previousEpoch = _currentEpoch;
                _hasPrevious = true;
                _currentBucket = new HashSet<string>();
                _currentEpoch = epoch;
                return;
            }

            if (epoch > _currentEpoch + 1)
            {
                _previousBucket = new HashSet<string>();
                _previousEpoch = long.MinValue;
                _hasPrevious = false;
                _currentBucket = new HashSet<string>();
                _currentEpoch = epoch;
                return;
            }
        }
    }
}
