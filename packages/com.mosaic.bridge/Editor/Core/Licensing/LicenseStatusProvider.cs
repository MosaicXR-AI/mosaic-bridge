using System;

namespace Mosaic.Bridge.Core.Licensing
{
    /// <summary>
    /// Provides observable license status by wrapping TrialManager.
    /// Implements ILicenseStatusProvider for dependency injection.
    /// </summary>
    public interface ILicenseStatusProvider
    {
        LicenseTier CurrentTier { get; }
        bool IsBlocked { get; }
        BlockReason? GetBlockReason();
        int DailyQuotaUsed { get; }
        int DailyQuota { get; }
        int TrialDaysRemaining { get; }

        /// <summary>Fires when RecordToolCall() changes the blocked state.</summary>
        event Action<LicenseTier> StatusChanged;

        /// <summary>Records a tool call. Returns true if allowed, false if blocked.</summary>
        bool RecordToolCall();
    }

    /// <summary>
    /// MVP implementation that delegates to TrialManager.
    /// Fires StatusChanged when RecordToolCall() transitions from unblocked to blocked.
    /// </summary>
    public sealed class LicenseStatusProvider : ILicenseStatusProvider
    {
        private readonly TrialManager _trial;

        public LicenseStatusProvider(TrialManager trial)
        {
            _trial = trial ?? throw new ArgumentNullException(nameof(trial));
        }

        public LicenseTier CurrentTier => _trial.CurrentTier;
        public bool IsBlocked => _trial.IsBlocked;
        public BlockReason? GetBlockReason() => _trial.GetBlockReason();
        public int DailyQuotaUsed => _trial.DailyQuotaUsed;
        public int DailyQuota => _trial.DailyQuota;
        public int TrialDaysRemaining => _trial.TrialDaysRemaining;

        public event Action<LicenseTier> StatusChanged;

        public bool RecordToolCall()
        {
            var wasBlocked = _trial.IsBlocked;
            var allowed = _trial.RecordToolCall();

            if (!wasBlocked && _trial.IsBlocked)
            {
                StatusChanged?.Invoke(_trial.CurrentTier);
            }

            return allowed;
        }
    }
}
