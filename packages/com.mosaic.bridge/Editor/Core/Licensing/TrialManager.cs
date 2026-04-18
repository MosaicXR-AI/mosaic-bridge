using System;
using UnityEditor;

namespace Mosaic.Bridge.Core.Licensing
{
    public sealed class TrialManager
    {
        private const string ActivatedAtKey = "MosaicBridge.TrialActivatedAt";
        private const string DailyCountKey = "MosaicBridge.DailyCallCount";
        private const string DailyDateKey = "MosaicBridge.DailyCallDate";
        private const int TrialDurationDays = 14;
        private const int DailyQuotaLimit = 50;

        public LicenseTier CurrentTier
        {
            get
            {
                if (IsTrialExpired) return LicenseTier.Expired;
                return LicenseTier.Trial;
            }
        }

        public bool IsTrialActive => !IsTrialExpired;

        public int TrialDaysRemaining
        {
            get
            {
                var activated = GetActivationDate();
                var remaining = TrialDurationDays - (DateTime.UtcNow - activated).Days;
                return Math.Max(0, remaining);
            }
        }

        public int DailyQuotaUsed => GetDailyCount();
        public int DailyQuota => DailyQuotaLimit;

        public bool IsBlocked
        {
            get
            {
                if (IsTrialExpired) return true;
                if (GetDailyCount() >= DailyQuotaLimit) return true;
                return false;
            }
        }

        public BlockReason? GetBlockReason()
        {
            if (IsTrialExpired) return BlockReason.TrialExpired;
            if (GetDailyCount() >= DailyQuotaLimit) return BlockReason.QuotaExhausted;
            return null;
        }

        /// <summary>Records a tool call. Returns true if allowed, false if blocked.</summary>
        public bool RecordToolCall()
        {
            if (IsBlocked) return false;
            ResetDailyCountIfNewDay();
            var count = EditorPrefs.GetInt(DailyCountKey, 0) + 1;
            EditorPrefs.SetInt(DailyCountKey, count);
            return count <= DailyQuotaLimit;
        }

        private bool IsTrialExpired
        {
            get
            {
                var activated = GetActivationDate();
                return (DateTime.UtcNow - activated).TotalDays > TrialDurationDays;
            }
        }

        private DateTime GetActivationDate()
        {
            var stored = EditorPrefs.GetString(ActivatedAtKey, "");
            if (string.IsNullOrEmpty(stored))
            {
                // First launch -- initialize trial
                var now = DateTime.UtcNow;
                EditorPrefs.SetString(ActivatedAtKey, now.ToString("O"));
                return now;
            }
            return DateTime.Parse(stored, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        private int GetDailyCount()
        {
            ResetDailyCountIfNewDay();
            return EditorPrefs.GetInt(DailyCountKey, 0);
        }

        private void ResetDailyCountIfNewDay()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var storedDate = EditorPrefs.GetString(DailyDateKey, "");
            if (storedDate != today)
            {
                EditorPrefs.SetString(DailyDateKey, today);
                EditorPrefs.SetInt(DailyCountKey, 0);
            }
        }
    }
}
