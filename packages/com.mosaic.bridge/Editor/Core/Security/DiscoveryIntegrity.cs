using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mosaic.Bridge.Core.Security
{
    /// <summary>
    /// Signs and verifies the discovery file using HMAC-SHA256 so that consumers can detect
    /// tampering. The signature covers all JSON fields except the <c>signature</c> field itself.
    /// </summary>
    public static class DiscoveryIntegrity
    {
        // Matches the "signature" key/value pair (with optional trailing comma) in pretty-printed JSON.
        // Handles both with-trailing-comma and trailing-comma-before-closing-brace cases.
        private static readonly Regex SignatureFieldRegex = new Regex(
            @"\s*""signature""\s*:\s*""[^""]*""\s*,?\s*",
            RegexOptions.Compiled);

        /// <summary>
        /// Compute HMAC-SHA256 signature over discovery file content (minus the signature field itself).
        /// Returns the signature as a lowercase hex string.
        /// </summary>
        /// <param name="discoveryJson">The full discovery JSON (may or may not contain a signature field).</param>
        /// <param name="secret">The 32-byte shared HMAC secret.</param>
        /// <returns>Lowercase hex-encoded HMAC-SHA256 signature.</returns>
        public static string Sign(string discoveryJson, byte[] secret)
        {
            if (string.IsNullOrEmpty(discoveryJson))
                throw new ArgumentNullException(nameof(discoveryJson));
            if (secret == null || secret.Length == 0)
                throw new ArgumentException("Secret must not be null or empty.", nameof(secret));

            var payload = StripSignatureField(discoveryJson);

            using (var hmac = new HMACSHA256(secret))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Verify a discovery file's HMAC-SHA256 signature using constant-time comparison.
        /// </summary>
        /// <param name="discoveryJson">The full discovery JSON including the signature field.</param>
        /// <param name="secret">The 32-byte shared HMAC secret.</param>
        /// <returns><c>true</c> if the signature is present and valid; <c>false</c> otherwise.</returns>
        public static bool Verify(string discoveryJson, byte[] secret)
        {
            if (string.IsNullOrEmpty(discoveryJson) || secret == null || secret.Length == 0)
                return false;

            // Extract the existing signature value from JSON
            var existingSignature = ExtractSignatureValue(discoveryJson);
            if (string.IsNullOrEmpty(existingSignature))
                return false;

            var expectedSignature = Sign(discoveryJson, secret);

            // Constant-time comparison
            if (existingSignature.Length != expectedSignature.Length)
                return false;

            var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
            var actualBytes = Encoding.UTF8.GetBytes(existingSignature);

            return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
        }

        /// <summary>
        /// Removes the "signature" field from JSON so that the remaining content can be signed/verified.
        /// Also cleans up any trailing comma issues that removal might cause.
        /// </summary>
        internal static string StripSignatureField(string json)
        {
            var stripped = SignatureFieldRegex.Replace(json, string.Empty);

            // Fix trailing comma before closing brace: ,\s*} => }
            stripped = Regex.Replace(stripped, @",\s*}", "}");

            return stripped;
        }

        /// <summary>
        /// Extracts the value of the "signature" field from discovery JSON.
        /// </summary>
        private static string ExtractSignatureValue(string json)
        {
            var match = Regex.Match(json, @"""signature""\s*:\s*""([^""]*)""");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
