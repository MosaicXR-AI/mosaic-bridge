using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Mosaic.Bridge.Contracts.Interfaces;

namespace Mosaic.Bridge.Core.Authentication
{
    /// <summary>
    /// Authenticates inbound bridge requests by verifying the HMAC-SHA256 signature carried
    /// in the <c>X-Mosaic-*</c> headers. Enforces clock skew, replay protection, and a body
    /// size limit, then performs a constant-time comparison of the computed and supplied
    /// signatures.
    /// </summary>
    public sealed class HmacAuthenticator
    {
        public const long MaxBodyBytes = 10L * 1024L * 1024L; // 10 MB
        public const int MaxClockSkewSeconds = 30;

        private const string NonceHeader = "X-Mosaic-Nonce";
        private const string TimestampHeader = "X-Mosaic-Timestamp";
        private const string SignatureHeader = "X-Mosaic-Signature";

        private readonly byte[] _secret;
        private readonly NonceCache _nonceCache;
        private readonly IMosaicLogger _logger;

        public HmacAuthenticator(byte[] secret, NonceCache nonceCache, IMosaicLogger logger)
        {
            if (secret == null) throw new ArgumentNullException(nameof(secret));
            if (secret.Length == 0) throw new ArgumentException("Secret must not be empty.", nameof(secret));
            _secret = secret;
            _nonceCache = nonceCache ?? throw new ArgumentNullException(nameof(nonceCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Authenticates a real <see cref="HttpListenerRequest"/> by adapting it to the
        /// internal <see cref="IHttpRequest"/> abstraction used for testability.
        /// </summary>
        public AuthResult Authenticate(HttpListenerRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return Authenticate(new HttpListenerRequestAdapter(request));
        }

        /// <summary>
        /// Authenticates a request expressed via the <see cref="IHttpRequest"/> abstraction.
        /// This is the path used by tests; production code reaches it through the
        /// <see cref="HttpListenerRequest"/> overload above.
        /// </summary>
        public AuthResult Authenticate(IHttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var nonce = request.GetHeader(NonceHeader);
            if (string.IsNullOrEmpty(nonce))
            {
                return Fail("missing_nonce");
            }

            var timestampHeader = request.GetHeader(TimestampHeader);
            if (string.IsNullOrEmpty(timestampHeader))
            {
                return Fail("missing_timestamp");
            }

            if (!long.TryParse(timestampHeader, out var timestamp))
            {
                return Fail("invalid_timestamp");
            }

            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(nowSeconds - timestamp) > MaxClockSkewSeconds)
            {
                return Fail("clock_skew");
            }

            byte[] body;
            try
            {
                body = ReadBody(request);
            }
            catch (BodyTooLargeException)
            {
                return Fail("body_too_large");
            }

            var bodySha = HmacCanonicalizer.ComputeBodySha256(body);
            var method = (request.HttpMethod ?? string.Empty).ToUpperInvariant();
            var path = request.RawUrl ?? string.Empty;
            var canonical = HmacCanonicalizer.Canonicalize(nonce, timestampHeader, method, path, bodySha);

            var signatureHeader = request.GetHeader(SignatureHeader);
            if (string.IsNullOrEmpty(signatureHeader))
            {
                return Fail("missing_signature");
            }

            if (!TryDecodeHex(signatureHeader, out var providedSignature))
            {
                return Fail("invalid_signature_format");
            }

            byte[] expectedSignature;
            using (var hmac = new HMACSHA256(_secret))
            {
                expectedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            }

            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature))
            {
                return Fail("signature_mismatch");
            }

            if (!_nonceCache.TryConsume(nonce, timestamp))
            {
                return Fail("nonce_replayed");
            }

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.Trace("hmac_auth_ok", ("path", path), ("method", method));
            }
            return AuthResult.Ok(body);
        }

        private AuthResult Fail(string reason)
        {
            _logger.Warn("hmac_auth_failed", ("reason", reason));
            return AuthResult.Fail(reason);
        }

        private static byte[] ReadBody(IHttpRequest request)
        {
            var declared = request.ContentLength64;
            if (declared > MaxBodyBytes)
            {
                throw new BodyTooLargeException();
            }

            var stream = request.InputStream;
            if (stream == null)
            {
                return Array.Empty<byte>();
            }

            // Cap how many bytes we will read regardless of declared content length, so a
            // dishonest Content-Length cannot lure us past the limit.
            var hardCap = MaxBodyBytes + 1;
            using (var ms = new MemoryStream())
            {
                var buffer = new byte[8192];
                long total = 0;
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += read;
                    if (total > MaxBodyBytes)
                    {
                        throw new BodyTooLargeException();
                    }
                    ms.Write(buffer, 0, read);
                    if (total >= hardCap)
                    {
                        throw new BodyTooLargeException();
                    }
                }
                return ms.ToArray();
            }
        }

        private static bool TryDecodeHex(string hex, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(hex) || (hex.Length % 2) != 0)
            {
                return false;
            }

            var result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                if (!TryParseHexNibble(hex[i * 2], out var hi) ||
                    !TryParseHexNibble(hex[i * 2 + 1], out var lo))
                {
                    return false;
                }
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
        /// Result of an authentication attempt. On success, <see cref="Body"/> carries the
        /// request body bytes that were read during HMAC verification (the stream is exhausted
        /// after auth, so the body must be forwarded here rather than re-read).
        /// </summary>
        public readonly struct AuthResult
        {
            public bool IsAuthenticated { get; }
            public string FailureReason { get; }
            /// <summary>
            /// The request body bytes read during authentication. Non-null only when
            /// <see cref="IsAuthenticated"/> is <c>true</c>; null on failure.
            /// </summary>
            public byte[] Body { get; }

            private AuthResult(bool authenticated, string reason, byte[] body)
            {
                IsAuthenticated = authenticated;
                FailureReason = reason;
                Body = body;
            }

            /// <summary>Backward-compatible overload — body carried as null.</summary>
            public static AuthResult Ok() => new AuthResult(true, null, null);
            /// <summary>Success overload that forwards the body bytes read during auth.</summary>
            public static AuthResult Ok(byte[] body) => new AuthResult(true, null, body);
            public static AuthResult Fail(string reason) => new AuthResult(false, reason, null);
        }

        private sealed class HttpListenerRequestAdapter : IHttpRequest
        {
            private readonly HttpListenerRequest _inner;

            public HttpListenerRequestAdapter(HttpListenerRequest inner)
            {
                _inner = inner;
            }

            public string HttpMethod => _inner.HttpMethod;
            public string RawUrl => _inner.RawUrl;
            public Stream InputStream => _inner.InputStream;
            public long ContentLength64 => _inner.ContentLength64;
            public string GetHeader(string name) => _inner.Headers[name];
        }
    }

    /// <summary>
    /// Minimal abstraction over an HTTP request used by <see cref="HmacAuthenticator"/>.
    /// Public to allow tests to construct fake requests without instantiating
    /// <see cref="HttpListenerRequest"/>, which is sealed and binds to OS sockets.
    /// </summary>
    public interface IHttpRequest
    {
        string HttpMethod { get; }
        string RawUrl { get; }
        Stream InputStream { get; }
        long ContentLength64 { get; }
        string GetHeader(string name);
    }
}
