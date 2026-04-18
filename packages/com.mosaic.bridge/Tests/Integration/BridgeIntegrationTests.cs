using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Mosaic.Bridge.Core.Bootstrap;
using Mosaic.Bridge.Core.Authentication;

namespace Mosaic.Bridge.Tests.Integration
{
    [TestFixture]
    [Category("Integration")]
    public class BridgeIntegrationTests
    {
        private HttpClient _http;
        private int _createdInstanceId;

        [SetUp]
        public void SetUp()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _createdInstanceId = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _http?.Dispose();
            if (_createdInstanceId != 0)
            {
#pragma warning disable CS0618
                var go = UnityEditor.EditorUtility.InstanceIDToObject(_createdInstanceId)
                         as UnityEngine.GameObject;
#pragma warning restore CS0618
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
                _createdInstanceId = 0;
            }
        }

        [Test]
        public void Bootstrap_IsRunning_BeforeTests()
        {
            Assert.AreEqual(BridgeState.Running, BridgeBootstrap.State,
                "BridgeBootstrap must be Running before integration tests execute. " +
                "If this fails, check the Unity Console for bootstrap errors.");
        }

        [Test]
        public void Health_Returns200()
        {
            Assert.AreEqual(BridgeState.Running, BridgeBootstrap.State);
            var url = $"http://127.0.0.1:{BridgeBootstrap.Server.Port}/health";
            var response = DriveUntilComplete(() => _http.GetAsync(url));
            Assert.AreEqual(200, (int)response.StatusCode);
        }

        [Test]
        public void Execute_GameObjectCreate_Returns200AndCreatesObject()
        {
            Assert.AreEqual(BridgeState.Running, BridgeBootstrap.State);

            const string goName = "__MosaicTest_GameObjectCreate__";
            // Route is "gameobject/create"; ToolRegistry normalizes / → _ so registered
            // name is "mosaic_gameobject_create".
            string body = $"{{\"tool\":\"mosaic_gameobject_create\"," +
                          $"\"parameters\":{{\"Name\":\"{goName}\"," +
                          $"\"Position\":[1.0,2.0,3.0]}}}}";

            var response = DriveUntilComplete(() => SendSigned("POST", "/execute", body));

            Assert.AreEqual(200, (int)response.StatusCode,
                $"Expected 200 but got {(int)response.StatusCode}. Body: {response.Content.ReadAsStringAsync().Result}");

            var json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            Assert.IsTrue((bool)json["success"], "ToolResult.success should be true");
            Assert.AreEqual(goName, (string)json["data"]["Name"]);

            _createdInstanceId = (int)json["data"]["InstanceId"];
        }

        [Test]
        public void Execute_MissingRequiredParam_Returns400()
        {
            Assert.AreEqual(BridgeState.Running, BridgeBootstrap.State);

            // No "Name" parameter — Required attribute should fail validation
            string body = "{\"tool\":\"mosaic_gameobject_create\",\"parameters\":{}}";
            var response = DriveUntilComplete(() => SendSigned("POST", "/execute", body));
            Assert.AreEqual(400, (int)response.StatusCode);
        }

        [Test]
        public void Execute_UnknownTool_Returns404()
        {
            Assert.AreEqual(BridgeState.Running, BridgeBootstrap.State);
            string body = "{\"tool\":\"mosaic_does_not_exist\",\"parameters\":{}}";
            var response = DriveUntilComplete(() => SendSigned("POST", "/execute", body));
            Assert.AreEqual(404, (int)response.StatusCode);
        }

        [Test]
        public void Execute_NoAuth_Returns401()
        {
            Assert.AreEqual(BridgeState.Running, BridgeBootstrap.State);
            var url = $"http://127.0.0.1:{BridgeBootstrap.Server.Port}/execute";
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            // No HMAC headers — BridgeServer rejects before reaching dispatcher
            var responseTask = Task.Run(() => _http.PostAsync(url, content).Result);
            responseTask.Wait(TimeSpan.FromSeconds(5));
            Assert.AreEqual(401, (int)responseTask.Result.StatusCode);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        // Drives MainThreadDispatcher.ProcessPendingRequests on the calling (main) thread
        // while the task runs on a background thread. Prevents deadlock.
        private HttpResponseMessage DriveUntilComplete(Func<Task<HttpResponseMessage>> factory)
        {
            var task = Task.Run(factory);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            {
                BridgeBootstrap.Dispatcher?.ProcessPendingRequests(maxToProcess: 5);
                Thread.Sleep(20);
            }
            Assert.IsTrue(task.IsCompleted, "Request did not complete within 10 seconds");
            return task.GetAwaiter().GetResult();
        }

        private Task<HttpResponseMessage> SendSigned(string method, string path, string bodyStr)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyStr);
            string nonce = Guid.NewGuid().ToString("N");
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string bodySha256 = HmacCanonicalizer.ComputeBodySha256(bodyBytes);
            string canonical = HmacCanonicalizer.Canonicalize(
                nonce, timestamp, method.ToUpperInvariant(), path, bodySha256);

            byte[] sigBytes;
            using (var hmac = new HMACSHA256(BridgeBootstrap.Secret))
                sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            string signature = BitConverter.ToString(sigBytes).Replace("-", "").ToLowerInvariant();

            var url = $"http://127.0.0.1:{BridgeBootstrap.Server.Port}{path}";
            var request = new HttpRequestMessage(
                method == "POST" ? HttpMethod.Post : HttpMethod.Get, url);
            if (bodyStr != null)
                request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Mosaic-Nonce", nonce);
            request.Headers.Add("X-Mosaic-Timestamp", timestamp);
            request.Headers.Add("X-Mosaic-Signature", signature);
            return _http.SendAsync(request);
        }
    }
}
