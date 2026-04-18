using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Authentication;
using Mosaic.Bridge.Core.Server;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mosaic.Bridge.Tests.Server
{
    [TestFixture]
    [Category("Integration")]
    public class BridgeServerTests
    {
        private static readonly byte[] TestSecret = Encoding.UTF8.GetBytes("test-secret-test-secret-test-12");

        private NonceCache _nonceCache;
        private RecordingLogger _logger;
        private HmacAuthenticator _authenticator;
        private BridgeServer _server;
        private HttpClient _httpClient;
        private StubHandler _stubHandler;

        [SetUp]
        public void SetUp()
        {
            // Server Stop() causes HttpListener.GetContext() to throw on the listen thread —
            // this is expected and not actionable. Suppress for all server tests.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            _nonceCache = new NonceCache();
            _logger = new RecordingLogger();
            _authenticator = new HmacAuthenticator(TestSecret, _nonceCache, _logger);
            _server = new BridgeServer(_authenticator, _logger);
            _stubHandler = new StubHandler();
            _httpClient = new HttpClient();
            _server.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _server.Stop();
            _httpClient.Dispose();
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        [Test]
        public void Start_BindsToNonZeroPort()
        {
            Assert.Greater(_server.Port, 0);
            Assert.IsTrue(_server.IsRunning);
        }

        [Test]
        public void Stop_SetsIsRunningFalse()
        {
            _server.Stop();
            Assert.IsFalse(_server.IsRunning);
            // Prevent TearDown from double-stopping — recreate so TearDown has something
            _server = new BridgeServer(_authenticator, _logger);
            _server.Start();
        }

        // ── Authentication ─────────────────────────────────────────────────────

        [Test]
        public async Task Request_NoAuthHeaders_Returns401()
        {
            var msg = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_server.Port}/api/test");
            var response = await _httpClient.SendAsync(msg);
            Assert.AreEqual(401, (int)response.StatusCode);
        }

        [Test]
        public async Task Request_WrongSignature_Returns401()
        {
            var wrongSecret = Encoding.UTF8.GetBytes("wrong-secret-wrong-secret-wrong!");
            var msg = MakeSignedRequest("POST", "/api/test", string.Empty, wrongSecret);
            var response = await _httpClient.SendAsync(msg);
            Assert.AreEqual(401, (int)response.StatusCode);
        }

        // ── Dispatching ────────────────────────────────────────────────────────

        [Test]
        public async Task Request_ValidHmac_NoHandler_Returns503()
        {
            // No handler set — server was created without one in SetUp.
            var msg = MakeSignedRequest("POST", "/api/test", string.Empty, TestSecret);
            var response = await _httpClient.SendAsync(msg);
            Assert.AreEqual(503, (int)response.StatusCode);
        }

        [Test]
        public async Task Request_ValidHmac_WithHandler_ReturnsHandlerResponse()
        {
            _stubHandler.Response = new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = "{\"ok\":true}"
            };
            _server.SetHandler(_stubHandler);

            var msg = MakeSignedRequest("POST", "/api/test", string.Empty, TestSecret);
            var response = await _httpClient.SendAsync(msg);

            Assert.AreEqual(200, (int)response.StatusCode);
        }

        [Test]
        public async Task Request_ValidHmac_HandlerBodyIsAvailable()
        {
            const string sentBody = "{\"hello\":\"world\"}";
            _stubHandler.Response = new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = "{}"
            };
            _server.SetHandler(_stubHandler);

            var msg = MakeSignedRequest("POST", "/api/test", sentBody, TestSecret);
            var response = await _httpClient.SendAsync(msg);

            Assert.AreEqual(200, (int)response.StatusCode);
            Assert.IsNotNull(_stubHandler.LastRequest);
            Assert.IsNotNull(_stubHandler.LastRequest.Body);

            var receivedBody = Encoding.UTF8.GetString(_stubHandler.LastRequest.Body);
            Assert.AreEqual(sentBody, receivedBody);
        }

        [Test]
        public async Task SetHandler_ReplacesHandlerWithoutRestart()
        {
            // First call: no handler → 503
            var msg1 = MakeSignedRequest("POST", "/api/test", string.Empty, TestSecret);
            var r1 = await _httpClient.SendAsync(msg1);
            Assert.AreEqual(503, (int)r1.StatusCode);

            // Set handler → next call should get 200
            _stubHandler.Response = new HandlerResponse { StatusCode = 200, ContentType = "application/json", Body = "{}" };
            _server.SetHandler(_stubHandler);

            // Use a fresh nonce via a new signed request
            var msg2 = MakeSignedRequest("POST", "/api/test", string.Empty, TestSecret);
            var r2 = await _httpClient.SendAsync(msg2);
            Assert.AreEqual(200, (int)r2.StatusCode);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private HttpRequestMessage MakeSignedRequest(
            string method, string url, string body, byte[] secret)
        {
            var bodyBytes = string.IsNullOrEmpty(body)
                ? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(body);

            var nonce = Guid.NewGuid().ToString();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var bodySha = HmacCanonicalizer.ComputeBodySha256(bodyBytes);
            var canonical = HmacCanonicalizer.Canonicalize(
                nonce, timestamp, method.ToUpperInvariant(), url, bodySha);

            string signatureHex;
            using (var hmac = new HMACSHA256(secret))
            {
                var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                signatureHex = ToLowerHex(sig);
            }

            var msg = new HttpRequestMessage(new HttpMethod(method),
                $"http://127.0.0.1:{_server.Port}{url}");
            msg.Headers.Add("X-Mosaic-Nonce", nonce);
            msg.Headers.Add("X-Mosaic-Timestamp", timestamp);
            msg.Headers.Add("X-Mosaic-Signature", signatureHex);

            if (bodyBytes.Length > 0)
                msg.Content = new ByteArrayContent(bodyBytes);

            return msg;
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        // ── Test doubles ───────────────────────────────────────────────────────

        private sealed class StubHandler : IRequestHandler
        {
            public HandlerResponse Response { get; set; } = new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = "{}"
            };

            public HandlerRequest LastRequest { get; private set; }

            public Task<HandlerResponse> HandleAsync(HandlerRequest request, CancellationToken ct)
            {
                LastRequest = request;
                return Task.FromResult(Response);
            }
        }

        private sealed class RecordingLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
            public List<string> Infos { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();

            public void Trace(string message, params (string Key, object Value)[] context) { }
            public void Debug(string message, params (string Key, object Value)[] context) { }
            public void Info(string message, params (string Key, object Value)[] context) { Infos.Add(message); }
            public void Warn(string message, params (string Key, object Value)[] context) { Warnings.Add(message); }
            public void Error(string message, Exception exception = null, params (string Key, object Value)[] context) { }
            public bool IsEnabled(LogLevel level) => level >= MinimumLevel;
        }
    }
}
