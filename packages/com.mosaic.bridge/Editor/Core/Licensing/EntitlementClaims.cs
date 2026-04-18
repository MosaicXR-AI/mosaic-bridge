using System;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Mosaic.Bridge.Core.Licensing
{
    /// <summary>
    /// Parses entitlement claims from a JWT activation token.
    /// MVP: base64 decode + JSON parse. No signature verification (deferred to Phase 2).
    /// </summary>
    public sealed class EntitlementClaims
    {
        private const string KeyActivationToken = "MosaicBridge.ActivationToken";

        public LicenseTier Tier { get; private set; } = LicenseTier.Trial;
        public bool Tier1Tools { get; private set; } = true;
        public bool Tier2Tools { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public string Subject { get; private set; }
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
        public bool IsValid { get; private set; }

        /// <summary>
        /// Loads and parses the stored activation token from EditorPrefs.
        /// Returns null if no token is stored.
        /// </summary>
        public static EntitlementClaims LoadFromEditorPrefs()
        {
            var token = EditorPrefs.GetString(KeyActivationToken, "");
            if (string.IsNullOrEmpty(token)) return null;
            return Parse(token);
        }

        /// <summary>
        /// Parses a JWT token string into entitlement claims.
        /// Returns claims with IsValid=false if parsing fails.
        /// </summary>
        public static EntitlementClaims Parse(string jwtToken)
        {
            var claims = new EntitlementClaims();

            try
            {
                // JWT format: header.payload.signature
                var parts = jwtToken.Split('.');
                if (parts.Length != 3)
                {
                    claims.IsValid = false;
                    return claims;
                }

                // Decode payload (part[1])
                var payload = Base64UrlDecode(parts[1]);
                var json = JObject.Parse(payload);

                // Extract standard claims
                claims.Subject = json["sub"]?.Value<string>();

                var exp = json["exp"]?.Value<long>();
                if (exp.HasValue)
                    claims.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(exp.Value).UtcDateTime;

                // Extract custom claims
                var tierStr = json["tier"]?.Value<string>();
                if (!string.IsNullOrEmpty(tierStr) && Enum.TryParse<LicenseTier>(tierStr, true, out var tier))
                    claims.Tier = tier;

                claims.Tier1Tools = json["entitlement.tier1Tools"]?.Value<bool>() ?? true;
                claims.Tier2Tools = json["entitlement.tier2Tools"]?.Value<bool>() ?? false;

                claims.IsValid = !claims.IsExpired;
            }
            catch
            {
                claims.IsValid = false;
            }

            return claims;
        }

        /// <summary>
        /// Checks if a specific tool tier is entitled.
        /// </summary>
        public bool IsToolTierAllowed(int tierLevel)
        {
            switch (tierLevel)
            {
                case 1: return Tier1Tools;
                case 2: return Tier2Tools;
                default: return false;
            }
        }

        private static string Base64UrlDecode(string input)
        {
            var padded = input.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            var bytes = Convert.FromBase64String(padded);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
