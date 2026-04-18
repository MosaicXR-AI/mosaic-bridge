using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Authentication;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Authentication
{
    [TestFixture]
    public class HmacAuthenticatorTests
    {
        private byte[] _secret;
        private NonceCache _cache;
        private RecordingLogger _logger;
        private HmacAuthenticator _auth;

        [SetUp]
        public void SetUp()
        {
            _secret = Encoding.UTF8.GetBytes("test-secret-test-secret-test-12");
            _cache = new NonceCache();
            _logger = new RecordingLogger();
            _auth = new HmacAuthenticator(_secret, _cache, _logger);
        }

        [Test]
        public void Authenticate_ValidRequest_Succeeds()
        {
            var req = BuildSignedRequest("POST", "/api/run-tool", Encoding.UTF8.GetBytes("{\"k\":1}"), "nonce-1");
            var result = _auth.Authenticate(req);

            Assert.IsTrue(result.IsAuthenticated, "expected success but got: " + result.FailureReason);
            Assert.IsNull(result.FailureReason);
        }

        [Test]
        public void Authenticate_MissingNonce_Fails()
        {
            var req = BuildSignedRequest("POST", "/api/run-tool", Array.Empty<byte>(), "n");
            req.RemoveHeader("X-Mosaic-Nonce");

            var result = _auth.Authenticate(req);

            Assert.IsFalse(result.IsAuthenticated);
            Assert.AreEqual("missing_nonce", result.FailureReason);
        }

        [Test]
        public void Authenticate_ClockSkewBeyondLimit_Fails()
        {
            var nowMinus5Min = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 300;
            var req = BuildSignedRequestAtTime("POST", "/api/run-tool", Array.Empty<byte>(), "n", nowMinus5Min);

            var result = _auth.Authenticate(req);

            Assert.IsFalse(result.IsAuthenticated);
            Assert.AreEqual("clock_skew", result.FailureReason);
        }

        [Test]
        public void Authenticate_ReplayedNonce_Fails()
        {
            // Same nonce, two distinct requests at the same wall-clock second.
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var first = BuildSignedRequestAtTime("POST", "/api/run-tool", Array.Empty<byte>(), "replay-1", ts);
            var second = BuildSignedRequestAtTime("POST", "/api/run-tool", Array.Empty<byte>(), "replay-1", ts);

            Assert.IsTrue(_auth.Authenticate(first).IsAuthenticated);

            var result = _auth.Authenticate(second);
            Assert.IsFalse(result.IsAuthenticated);
            Assert.AreEqual("nonce_replayed", result.FailureReason);
        }

        [Test]
        public void Authenticate_SignatureMismatch_Fails()
        {
            var req = BuildSignedRequest("POST", "/api/run-tool", Array.Empty<byte>(), "n");
            // Tamper with the body after signing — recompute should not match.
            req.SetBody(Encoding.UTF8.GetBytes("tampered"));

            var result = _auth.Authenticate(req);
            Assert.IsFalse(result.IsAuthenticated);
            Assert.AreEqual("signature_mismatch", result.FailureReason);
        }

        [Test]
        public void Authenticate_BodyDeclaredOverLimit_Fails()
        {
            var req = BuildSignedRequest("POST", "/api/run-tool", Array.Empty<byte>(), "n");
            req.ContentLength64 = HmacAuthenticator.MaxBodyBytes + 1;

            var result = _auth.Authenticate(req);
            Assert.IsFalse(result.IsAuthenticated);
            Assert.AreEqual("body_too_large", result.FailureReason);
        }

        [Test]
        public void Authenticate_BodyStreamExceedsLimitWithoutContentLength_Fails()
        {
            // No declared Content-Length; stream contains more than the limit.
            var oversized = new byte[HmacAuthenticator.MaxBodyBytes + 16];
            var req = new FakeHttpRequest
            {
                HttpMethod = "POST",
                RawUrl = "/api/run-tool",
                ContentLength64 = -1, // unknown
            };
            req.SetBody(oversized);

            // Sign the request as if the body were correct (signature won't matter — we
            // expect body_too_large to fire before signature verification).
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            req.SetHeader("X-Mosaic-Nonce", "n-large");
            req.SetHeader("X-Mosaic-Timestamp", ts);
            req.SetHeader("X-Mosaic-Signature", "00");

            var result = _auth.Authenticate(req);
            Assert.IsFalse(result.IsAuthenticated);
            Assert.AreEqual("body_too_large", result.FailureReason);
        }

        [Test]
        public void Authenticate_PathWithEmbeddedNewlineInRawUrl_StillAuthenticatesEndToEnd()
        {
            // Critical parser test: a literal \n inside the path must round-trip through
            // the length-prefixed canonical format. The authenticator must compute the same
            // HMAC the client did, and admit the request.
            const string evilPath = "/api/run-tool\u000Ainjected/path";

            var req = BuildSignedRequest("POST", evilPath, Encoding.UTF8.GetBytes("body"), "newline-nonce");
            var result = _auth.Authenticate(req);

            Assert.IsTrue(result.IsAuthenticated, "expected success but got: " + result.FailureReason);
        }

        [Test]
        public void Authenticate_MissingSignature_Fails()
        {
            var req = BuildSignedRequest("POST", "/api/run-tool", Array.Empty<byte>(), "n");
            req.RemoveHeader("X-Mosaic-Signature");

            var result = _auth.Authenticate(req);
            Assert.IsFalse(result.IsAuthenticated);
            Assert.AreEqual("missing_signature", result.FailureReason);
        }

        [Test]
        public void Authenticate_MalformedHexSignature_Fails()
        {
            var req = BuildSignedRequest("POST", "/api/run-tool", Array.Empty<byte>(), "n");
            req.SetHeader("X-Mosaic-Signature", "not-hex!");

            var result = _auth.Authenticate(req);
            Assert.IsFalse(result.IsAuthenticated);
            Assert.AreEqual("invalid_signature_format", result.FailureReason);
        }

        // ---------- helpers ----------

        private FakeHttpRequest BuildSignedRequest(string method, string rawUrl, byte[] body, string nonce)
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return BuildSignedRequestAtTime(method, rawUrl, body, nonce, ts);
        }

        private FakeHttpRequest BuildSignedRequestAtTime(
            string method, string rawUrl, byte[] body, string nonce, long timestamp)
        {
            var req = new FakeHttpRequest
            {
                HttpMethod = method,
                RawUrl = rawUrl,
                ContentLength64 = body?.Length ?? 0,
            };
            req.SetBody(body ?? Array.Empty<byte>());

            var tsStr = timestamp.ToString();
            var bodySha = HmacCanonicalizer.ComputeBodySha256(body);
            var canonical = HmacCanonicalizer.Canonicalize(nonce, tsStr, method.ToUpperInvariant(), rawUrl, bodySha);

            string signatureHex;
            using (var hmac = new HMACSHA256(_secret))
            {
                var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                signatureHex = ToLowerHex(sig);
            }

            req.SetHeader("X-Mosaic-Nonce", nonce);
            req.SetHeader("X-Mosaic-Timestamp", tsStr);
            req.SetHeader("X-Mosaic-Signature", signatureHex);
            return req;
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private sealed class FakeHttpRequest : IHttpRequest
        {
            private readonly Dictionary<string, string> _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private byte[] _body = Array.Empty<byte>();

            public string HttpMethod { get; set; }
            public string RawUrl { get; set; }
            public long ContentLength64 { get; set; }
            public Stream InputStream => new MemoryStream(_body, writable: false);

            public string GetHeader(string name)
            {
                return _headers.TryGetValue(name, out var v) ? v : null;
            }

            public void SetHeader(string name, string value) => _headers[name] = value;
            public void RemoveHeader(string name) => _headers.Remove(name);

            public void SetBody(byte[] body)
            {
                _body = body ?? Array.Empty<byte>();
            }
        }

        private sealed class RecordingLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
            public List<string> Warnings { get; } = new List<string>();

            public void Trace(string message, params (string Key, object Value)[] context) { }
            public void Debug(string message, params (string Key, object Value)[] context) { }
            public void Info(string message, params (string Key, object Value)[] context) { }
            public void Warn(string message, params (string Key, object Value)[] context) { Warnings.Add(message); }
            public void Error(string message, Exception exception = null, params (string Key, object Value)[] context) { }
            public bool IsEnabled(LogLevel level) => level >= MinimumLevel;
        }
    }
}
