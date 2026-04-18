using System;
using System.Globalization;
using UnityEditor;

namespace Mosaic.Bridge.Core.Licensing
{
    /// <summary>
    /// Tracks offline grace period for paid licenses. After activation,
    /// the plugin works offline for 7 days. After that, tool execution
    /// is blocked until the user reconnects.
    /// </summary>
    public sealed class OfflineGraceManager
    {
        private const string KeyLastValidated = "MosaicBridge.LastValidatedAt";
        private const string KeyLastSessionTime = "MosaicBridge.LastSessionTime";
        private const int GracePeriodDays = 7;
        private const int ClockTamperThresholdDays = 1;

        public Func<DateTime> UtcNowProvider { get; set; } = () => DateTime.UtcNow;

        /// <summary>When the grace period expires (null if online or no license).</summary>
        public DateTime? GraceExpiresAt
        {
            get
            {
                var lastValidated = GetLastValidatedAt();
                if (!lastValidated.HasValue) return null;
                return lastValidated.Value.AddDays(GracePeriodDays);
            }
        }

        /// <summary>True if the grace period has expired.</summary>
        public bool IsGraceExpired
        {
            get
            {
                var expires = GraceExpiresAt;
                if (!expires.HasValue) return false;
                return UtcNowProvider() > expires.Value;
            }
        }

        /// <summary>Days remaining in grace period (0 if expired, -1 if no license).</summary>
        public int GraceDaysRemaining
        {
            get
            {
                var expires = GraceExpiresAt;
                if (!expires.HasValue) return -1;
                var remaining = (expires.Value - UtcNowProvider()).Days;
                return Math.Max(0, remaining);
            }
        }

        /// <summary>
        /// Detects clock tampering: if system clock moved backward by more than 1 day.
        /// </summary>
        public bool IsClockTamperDetected
        {
            get
            {
                var lastSession = GetLastSessionTime();
                if (!lastSession.HasValue) return false;
                var diff = (lastSession.Value - UtcNowProvider()).TotalDays;
                return diff > ClockTamperThresholdDays;
            }
        }

        /// <summary>Records the current time as the last session time.</summary>
        public void RecordSessionTime()
        {
            EditorPrefs.SetString(KeyLastSessionTime,
                UtcNowProvider().ToString("O", CultureInfo.InvariantCulture));
        }

        /// <summary>Records a successful server validation.</summary>
        public void RecordValidation()
        {
            EditorPrefs.SetString(KeyLastValidated,
                UtcNowProvider().ToString("O", CultureInfo.InvariantCulture));
            RecordSessionTime();
        }

        private DateTime? GetLastValidatedAt()
        {
            var raw = EditorPrefs.GetString(KeyLastValidated, "");
            if (string.IsNullOrEmpty(raw)) return null;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt))
                return dt;
            return null;
        }

        private DateTime? GetLastSessionTime()
        {
            var raw = EditorPrefs.GetString(KeyLastSessionTime, "");
            if (string.IsNullOrEmpty(raw)) return null;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt))
                return dt;
            return null;
        }
    }
}
