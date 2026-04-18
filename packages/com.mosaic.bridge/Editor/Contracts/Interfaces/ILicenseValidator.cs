using System.Collections.Generic;

namespace Mosaic.Bridge.Contracts.Interfaces
{
    /// <summary>
    /// License validation interface.
    /// Per FR42-FR48 and NFR24b: validates JWT-based license tokens with capability-only claims.
    /// </summary>
    /// <remarks>
    /// MVP implementation is a stub validator that accepts properly-signed JWT files
    /// (per Decision A4). The full license server is a Phase 2 deliverable. The bridge code
    /// implements full validation logic so the server can be plugged in later without code changes.
    /// </remarks>
    public interface ILicenseValidator
    {
        /// <summary>
        /// Validate the current license and return its claims.
        /// Returns null if no license is present (trial mode applies).
        /// </summary>
        LicenseInfo Validate();

        /// <summary>
        /// Check whether a specific feature/capability is enabled by the current license.
        /// Per FR47: license entitlements are encoded as boolean claims in the activation token.
        /// </summary>
        bool HasEntitlement(string entitlementKey);

        /// <summary>
        /// True if the current license is in trial mode.
        /// </summary>
        bool IsTrial { get; }

        /// <summary>
        /// True if the trial has expired or the license has been invalidated.
        /// </summary>
        bool IsExpired { get; }

        /// <summary>
        /// Days remaining in the trial or until license expiry.
        /// Returns null if no expiry is set (perpetual licenses).
        /// </summary>
        int? DaysRemaining { get; }
    }

    /// <summary>
    /// Validated license information from the activation token.
    /// Per NFR24b: tokens contain exp, kid, iat, sub, tier, plus boolean capability claims.
    /// </summary>
    public class LicenseInfo
    {
        /// <summary>License tier identifier (trial, indie, pro, team, enterprise, pilot).</summary>
        public string Tier { get; set; }

        /// <summary>License holder identifier (email or organization).</summary>
        public string Subject { get; set; }

        /// <summary>JWT key ID (for key rotation support).</summary>
        public string KeyId { get; set; }

        /// <summary>Token issued-at timestamp (Unix seconds).</summary>
        public long IssuedAt { get; set; }

        /// <summary>Token expiration timestamp (Unix seconds). 0 if no expiry.</summary>
        public long ExpiresAt { get; set; }

        /// <summary>
        /// Boolean capability claims encoded in the token (per FR69).
        /// Examples: { "tier1Tools": true, "knowledgeBase": true, "instructorMode": true }
        /// </summary>
        public Dictionary<string, bool> Entitlements { get; set; } = new Dictionary<string, bool>();
    }
}
