using System;
using UnityEditor;

namespace Mosaic.Bridge.Core.Licensing
{
    /// <summary>
    /// Manages pilot license features: custom term length, telemetry flag,
    /// and tier restriction bypass.
    /// </summary>
    public sealed class PilotLicenseManager
    {
        private const string KeyPilotExpiry = "MosaicBridge.PilotExpiryAt";
        private const string KeyPilotTelemetry = "MosaicBridge.PilotTelemetryEnabled";

        /// <summary>Whether the current license is a pilot tier.</summary>
        public bool IsPilot => EditorPrefs.GetString("MosaicBridge.LicenseTier", "") == "pilot";

        /// <summary>Whether pilot telemetry is enabled.</summary>
        public bool TelemetryEnabled
        {
            get => IsPilot && EditorPrefs.GetBool(KeyPilotTelemetry, true);
            set => EditorPrefs.SetBool(KeyPilotTelemetry, value);
        }

        /// <summary>Custom pilot expiry date (null if no pilot license).</summary>
        public DateTime? PilotExpiresAt
        {
            get
            {
                var raw = EditorPrefs.GetString(KeyPilotExpiry, "");
                if (string.IsNullOrEmpty(raw)) return null;
                if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt;
                return null;
            }
            set
            {
                if (value.HasValue)
                    EditorPrefs.SetString(KeyPilotExpiry, value.Value.ToString("O"));
                else
                    EditorPrefs.DeleteKey(KeyPilotExpiry);
            }
        }

        /// <summary>Whether the pilot license has expired.</summary>
        public bool IsPilotExpired
        {
            get
            {
                if (!IsPilot) return false;
                var expiry = PilotExpiresAt;
                if (!expiry.HasValue) return false; // no expiry = permanent pilot
                return DateTime.UtcNow > expiry.Value;
            }
        }

        /// <summary>Activates a pilot license with custom term.</summary>
        public void ActivatePilot(DateTime? expiresAt = null)
        {
            EditorPrefs.SetString("MosaicBridge.LicenseTier", "pilot");
            if (expiresAt.HasValue)
                PilotExpiresAt = expiresAt.Value;
            TelemetryEnabled = true;
        }
    }
}
