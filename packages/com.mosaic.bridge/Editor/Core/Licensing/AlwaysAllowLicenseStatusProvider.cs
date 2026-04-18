using System;

namespace Mosaic.Bridge.Core.Licensing
{
    /// <summary>
    /// Development/testing stub that always reports an active trial with no blocking.
    /// </summary>
    public sealed class AlwaysAllowLicenseStatusProvider : ILicenseStatusProvider
    {
        private int _dailyQuotaUsed;

        public LicenseTier CurrentTier => LicenseTier.Trial;
        public bool IsBlocked => false;
        public BlockReason? GetBlockReason() => null;
        public int DailyQuotaUsed => _dailyQuotaUsed;
        public int DailyQuota => int.MaxValue;
        public int TrialDaysRemaining => 14;

        public event Action<LicenseTier> StatusChanged;

        public bool RecordToolCall()
        {
            _dailyQuotaUsed++;
            return true; // always allowed
        }
    }
}
