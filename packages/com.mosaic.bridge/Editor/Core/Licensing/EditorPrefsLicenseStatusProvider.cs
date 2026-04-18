using System;
using System.Globalization;
using UnityEditor;

namespace Mosaic.Bridge.Core.Licensing
{
    /// <summary>
    /// License status provider backed by EditorPrefs.
    /// Persists trial activation, daily quota counts, and license tier.
    /// </summary>
    public sealed class EditorPrefsLicenseStatusProvider : ILicenseStatusProvider
    {
        internal const string KeyTrialActivatedAt = "MosaicBridge.TrialActivatedAt";
        internal const string KeyDailyCallCount   = "MosaicBridge.DailyCallCount";
        internal const string KeyDailyCallDate    = "MosaicBridge.DailyCallDate";
        internal const string KeyLicenseTier      = "MosaicBridge.LicenseTier";

        internal const int TrialDurationDays = 14;
        internal const int TrialDailyQuotaLimit = 50;

        internal Func<DateTime> UtcNowProvider { get; set; } = () => DateTime.UtcNow;

        public event Action<LicenseTier> StatusChanged;

        public LicenseTier CurrentTier
        {
            get
            {
                var tierStr = EditorPrefs.GetString(KeyLicenseTier, "trial");
                if (Enum.TryParse<LicenseTier>(tierStr, ignoreCase: true, out var tier))
                    return tier;
                return LicenseTier.Trial;
            }
        }

        public int TrialDaysRemaining
        {
            get
            {
                if (CurrentTier != LicenseTier.Trial && CurrentTier != LicenseTier.Expired)
                    return 0;

                var activatedAt = GetTrialActivatedAt();
                if (!activatedAt.HasValue)
                {
                    ActivateTrial();
                    return TrialDurationDays;
                }

                var elapsed = (UtcNowProvider() - activatedAt.Value).TotalDays;
                var remaining = TrialDurationDays - (int)Math.Ceiling(elapsed);
                return Math.Max(0, remaining);
            }
        }

        public int DailyQuotaUsed
        {
            get
            {
                ResetDailyCounterIfNewDay();
                return EditorPrefs.GetInt(KeyDailyCallCount, 0);
            }
        }

        public int DailyQuota
        {
            get
            {
                if (CurrentTier == LicenseTier.Trial)
                    return TrialDailyQuotaLimit;
                return int.MaxValue;
            }
        }

        public bool IsBlocked => GetBlockReason().HasValue;

        public BlockReason? GetBlockReason()
        {
            var tier = CurrentTier;

            if (tier == LicenseTier.Expired)
                return Licensing.BlockReason.GraceExpired;

            if (tier == LicenseTier.Trial)
            {
                if (TrialDaysRemaining <= 0)
                    return Licensing.BlockReason.TrialExpired;

                if (DailyQuotaUsed >= TrialDailyQuotaLimit)
                    return Licensing.BlockReason.QuotaExhausted;
            }

            return null;
        }

        public bool RecordToolCall()
        {
            if (IsBlocked) return false;

            ResetDailyCounterIfNewDay();
            var count = EditorPrefs.GetInt(KeyDailyCallCount, 0) + 1;
            EditorPrefs.SetInt(KeyDailyCallCount, count);

            if (count >= TrialDailyQuotaLimit && CurrentTier == LicenseTier.Trial)
            {
                StatusChanged?.Invoke(CurrentTier);
                return false;
            }

            return true;
        }

        internal void ActivateTrial()
        {
            if (EditorPrefs.HasKey(KeyTrialActivatedAt))
                return;

            var now = UtcNowProvider().ToString("o", CultureInfo.InvariantCulture);
            EditorPrefs.SetString(KeyTrialActivatedAt, now);
            EditorPrefs.SetString(KeyLicenseTier, "trial");
        }

        private DateTime? GetTrialActivatedAt()
        {
            if (!EditorPrefs.HasKey(KeyTrialActivatedAt))
                return null;

            var raw = EditorPrefs.GetString(KeyTrialActivatedAt, "");
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt))
                return dt;

            return null;
        }

        private void ResetDailyCounterIfNewDay()
        {
            var today = UtcNowProvider().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var storedDate = EditorPrefs.GetString(KeyDailyCallDate, "");

            if (storedDate != today)
            {
                EditorPrefs.SetString(KeyDailyCallDate, today);
                EditorPrefs.SetInt(KeyDailyCallCount, 0);
            }
        }
    }
}
