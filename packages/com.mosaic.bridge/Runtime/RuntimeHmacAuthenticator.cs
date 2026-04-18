using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// HMAC-SHA256 authenticator for runtime HTTP requests.
    /// Mirrors the editor <c>HmacAuthenticator</c> using the same canonical format,
    /// nonce replay protection, and clock-skew enforcement.
    /// </summary>
    public sealed class RuntimeHmacAuthenticator
    {
        public const long MaxBodyBytes = 10L * 1024L * 1024L; // 10 MB
        public const int MaxClockSkewSeconds = 30;

        private const string NonceHeader = "X-Mosaic-Nonce";
        private const string TimestampHeader = "X-Mosaic-Timestamp";
        private const string SignatureHeader = "X-Mosaic-Signature";

        private readonly byte[] _secret;
        private readonly RuntimeNonceCache _nonceCache;
        private readonly RuntimeLogger _logger;

        public RuntimeHmacAuthenticator(byte[] secret, RuntimeNonceCache nonceCache, RuntimeLogger logger)
        {
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            if (secret.Length == 0) throw new ArgumentException("Secret must not be empty.", nameof(secret));
            _secret = secret;
            _nonceCache = nonceCache ?? throw new ArgumentNullException(nameof(nonceCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Authenticates a real <see cref="HttpListenerRequest"/>.
        /// Returns the authentication result including the body bytes read during verification.
        /// </summary>
        public AuthResult Authenticate(HttpListenerRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var nonce = request.Headers[NonceHeader];
            if (string.IsNullOrEmpty(nonce))
                return Fail("missing_nonce");

            var timestampHeader = request.Headers[TimestampHeader];
            if (string.IsNullOrEmpty(timestampHeader))
                return Fail("missing_timestamp");

            if (!long.TryParse(timestampHeader, out var timestamp))
                return Fail("invalid_timestamp");

            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(nowSeconds - timestamp) > MaxClockSkewSeconds)
                return Fail("clock_skew");

            byte[] body;
            try
            {
                body = ReadBody(request);
            }
            catch (BodyTooLargeException)
            {
                return Fail("body_too_large");
            }

            var bodySha = ComputeBodySha256(body);
            var method = (request.HttpMethod ?? string.Empty).ToUpperInvariant();
            var path = request.RawUrl ?? string.Empty;
            var canonical = Canonicalize(nonce, timestampHeader, method, path, bodySha);

            var signatureHeader = request.Headers[SignatureHeader];
            if (string.IsNullOrEmpty(signatureHeader))
                return Fail("missing_signature");

            if (!TryDecodeHex(signatureHeader, out var providedSignature))
                return Fail("invalid_signature_format");

            byte[] expectedSignature;
            using (var hmac = new HMACSHA256(_secret))
            {
                expectedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            }

            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature))
                return Fail("signature_mismatch");

            if (!_nonceCache.TryConsume(nonce, timestamp))
                return Fail("nonce_replayed");

            _logger.Trace("hmac_auth_ok");
            return AuthResult.Ok(body);
        }

        private AuthResult Fail(string reason)
        {
            _logger.Warn($"hmac_auth_failed: {reason}");
            return AuthResult.Fail(reason);
        }

        // --- Canonical format (same as editor HmacCanonicalizer) ---

        private const string CanonicalVersion = "v1";

        internal static string Canonicalize(string nonce, string timestamp, string method, string path, string bodySha256)
        {
            var sb = new StringBuilder();
            sb.Append(CanonicalVersion);
            AppendLengthPrefixedField(sb, nonce);
            AppendLengthPrefixedField(sb, timestamp);
            AppendLengthPrefixedField(sb, method);
            AppendLengthPrefixedField(sb, path);
            AppendLengthPrefixedField(sb, bodySha256);
            return sb.ToString();
        }

        internal static string ComputeBodySha256(byte[] body)
        {
            var input = body ?? Array.Empty<byte>();
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(input);
                return ToLowerHex(hash);
            }
        }

        private static void AppendLengthPrefixedField(StringBuilder sb, string field)
        {
            sb.Append('\n');
            sb.Append(Encoding.UTF8.GetByteCount(field));
            sb.Append(':');
            sb.Append(field);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            const string hex = "0123456789abcdef";
            var chars = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = hex[bytes[i] >> 4];
                chars[i * 2 + 1] = hex[bytes[i] & 0x0F];
            }
            return new string(chars);
        }

        private static byte[] ReadBody(HttpListenerRequest request)
        {
            if (request.ContentLength64 > MaxBodyBytes)
                throw new BodyTooLargeException();

            using (var ms = new MemoryStream())
            {
                var buffer = new byte[8192];
                long total = 0;
                int read;
                while ((read = request.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += read;
                    if (total > MaxBodyBytes)
                        throw new BodyTooLargeException();
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        private static bool TryDecodeHex(string hex, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(hex) || (hex.Length % 2) != 0)
                return false;

            var result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                if (!TryParseHexNibble(hex[i * 2], out var hi) ||
                    !TryParseHexNibble(hex[i * 2 + 1], out var lo))
                    return false;
                result[i] = (byte)((hi << 4) | lo);
            }

            bytes = result;
            return true;
        }

        private static bool TryParseHexNibble(char c, out int value)
        {
            if (c >= '0' && c <= '9') { value = c - '0'; return true; }
            if (c >= 'a' && c <= 'f') { value = 10 + (c - 'a'); return true; }
            if (c >= 'A' && c <= 'F') { value = 10 + (c - 'A'); return true; }
            value = 0;
            return false;
        }

        private sealed class BodyTooLargeException : Exception { }

        /// <summary>
        /// Result of an authentication attempt.
        /// </summary>
        public readonly struct AuthResult
        {
            public bool IsAuthenticated { get; }
            public string FailureReason { get; }
            public byte[] Body { get; }

            private AuthResult(bool authenticated, string reason, byte[] body)
            {
                IsAuthenticated = authenticated;
                FailureReason = reason;
                Body = body;
            }

            public static AuthResult Ok(byte[] body) => new AuthResult(true, null, body);
            public static AuthResult Fail(string reason) => new AuthResult(false, reason, null);
        }
    }
}
