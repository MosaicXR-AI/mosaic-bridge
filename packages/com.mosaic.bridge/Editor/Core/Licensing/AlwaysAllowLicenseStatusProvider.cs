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

        /// <summary>
        /// Required by the interface, never raised: this provider's status never changes.
        /// Declared with explicit accessors so the compiler does not warn (CS0067) about an
        /// event that is assigned and never used — in a customer's Console, since the
        /// Bridge is compiled from source there.
        /// </summary>
        public event Action<LicenseTier> StatusChanged
        {
            add { }
            remove { }
        }

        public bool RecordToolCall()
        {
            _dailyQuotaUsed++;
            return true; // always allowed
        }
    }
}
