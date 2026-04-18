using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mosaic.Bridge.Core.Authentication;
using Mosaic.Bridge.Core.Bootstrap;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Regression
{
    /// <summary>
    /// Loads JSON fixtures from the Fixtures/ directory and runs each one against
    /// the live Mosaic Bridge HTTP endpoint. Each fixture defines input parameters
    /// and expected output fields, producing one NUnit test per fixture file.
    ///
    /// Run requirements:
    ///   - BridgeBootstrap must be in the Running state (start Unity first).
    ///   - Use the Unity Test Runner "Edit Mode" tab, filter by Category "Regression".
    /// </summary>
    [TestFixture]
    [Category("Integration")]
    [Category("Regression")]
    public class RegressionTestRunner
    {
        private static readonly string FixturesPath = Path.GetFullPath(
            Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "Packages",
                "com.mosaic.bridge",
                "Tests",
                "Regression",
                "Fixtures"));

        private HttpClient _http;

        // ── Fixture discovery ────────────────────────────────────────────────

        public static IEnumerable<TestCaseData> GetFixtures()
        {
            if (!Directory.Exists(FixturesPath))
            {
                yield return new TestCaseData("<fixtures-dir-missing>")
                    .SetName("Regression — fixtures directory not found");
                yield break;
            }

            string[] files = Directory.GetFiles(FixturesPath, "*.json");
            Array.Sort(files);

            foreach (string path in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                yield return new TestCaseData(path).SetName($"Regression — {fileName}");
            }
        }

        // ── Setup / Teardown ─────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        }

        [TearDown]
        public void TearDown()
        {
            _http?.Dispose();
        }

        // ── Main test ────────────────────────────────────────────────────────

        [Test]
        [Explicit("Regression tests make real HTTP calls to the bridge — run individually, not with Run All")]
        [Timeout(30000)]
        [TestCaseSource(nameof(GetFixtures))]
        public void RunFixture(string fixturePath)
        {
            if (fixturePath == "<fixtures-dir-missing>")
                Assert.Fail("Fixtures directory was not found at: " + FixturesPath);

            // Skip gracefully if the bridge isn't running. Regression tests make real HTTP calls
            // to the bridge and can't do anything useful without a live listener. An Assert.Ignore
            // surfaces as a yellow "skipped" in Test Runner rather than a red "failure" — clearer
            // signal that this is an environment issue, not a code defect.
            if (BridgeBootstrap.State != BridgeState.Running)
            {
                Assert.Ignore(
                    "Bridge is not running. Start the Unity bridge before running regression tests.");
                return;
            }

            // 1. Load and parse fixture
            string json = File.ReadAllText(fixturePath);
            FixtureSchema fixture = JsonConvert.DeserializeObject<FixtureSchema>(json);
            Assert.IsNotNull(fixture, $"Failed to deserialize fixture: {fixturePath}");
            Assert.IsNotNull(fixture.Tool, $"Fixture '{fixturePath}' is missing the 'tool' field.");

            // Track objects created by setup steps for cleanup
            var createdObjects = new List<string>();

            try
            {
                // 2. Run setup steps (if any)
                var setupResults = new Dictionary<string, JObject>();
                if (fixture.Setup != null)
                {
                    foreach (var step in fixture.Setup)
                    {
                        var setupResponse = ExecuteTool(step.Tool, step.Parameters);
                        Assert.AreEqual(200, setupResponse.StatusCode,
                            $"Setup step '{step.Tool}' failed with status {setupResponse.StatusCode}: {setupResponse.Body}");

                        var setupBody = JObject.Parse(setupResponse.Body);
                        Assert.IsTrue(setupBody["success"]?.Value<bool>() == true,
                            $"Setup step '{step.Tool}' returned success=false: {setupResponse.Body}");

                        if (!string.IsNullOrEmpty(step.StoreAs))
                            setupResults[step.StoreAs] = setupBody;

                        // Track created objects for cleanup
                        var createdName = setupBody["data"]?["Name"]?.Value<string>();
                        if (!string.IsNullOrEmpty(createdName))
                            createdObjects.Add(createdName);
                    }
                }

                // 3. Execute the main tool — retry on 202 MAIN_THREAD_BLOCKED with
                // exponential backoff: 500ms, 1s, 2s, 4s, ... up to 15 retries
                // (~60s total). Fixtures that touch heavy main-thread work (UI
                // rebuilds, compilation, domain reloads) can block the queue for
                // extended periods.
                var response = ExecuteTool(fixture.Tool, fixture.Parameters);
                int blockedRetries = 0;
                const int maxRetries = 15;
                while (response.StatusCode == 202 && blockedRetries < maxRetries)
                {
                    int waitMs = System.Math.Min(4000, 500 * (1 << System.Math.Min(blockedRetries, 3)));
                    System.Threading.Thread.Sleep(waitMs);
                    response = ExecuteTool(fixture.Tool, fixture.Parameters);
                    blockedRetries++;
                }

                // 4. Verify expectations
                var expectations = fixture.Expectations;
                Assert.IsNotNull(expectations, $"Fixture '{fixture.Fixture}' is missing 'expectations'.");

                Assert.AreEqual(expectations.StatusCode, response.StatusCode,
                    $"Fixture '{fixture.Fixture}': expected status {expectations.StatusCode} but got {response.StatusCode} (after {blockedRetries} MAIN_THREAD_BLOCKED retries). Body: {response.Body}");

                var responseBody = JObject.Parse(response.Body);

                bool actualSuccess = responseBody["success"]?.Value<bool>() ?? false;
                Assert.AreEqual(expectations.Success, actualSuccess,
                    $"Fixture '{fixture.Fixture}': expected success={expectations.Success} but got {actualSuccess}. Body: {response.Body}");

                // Verify expected data fields are present
                if (expectations.DataFields != null && expectations.DataFields.Count > 0)
                {
                    var data = responseBody["data"];
                    Assert.IsNotNull(data,
                        $"Fixture '{fixture.Fixture}': expected data fields but response has no 'data' property. Body: {response.Body}");

                    foreach (string field in expectations.DataFields)
                    {
                        Assert.IsNotNull(data[field],
                            $"Fixture '{fixture.Fixture}': expected data field '{field}' is missing from response. Data: {data}");
                    }
                }

                // Verify exact data value checks
                if (expectations.DataChecks != null && expectations.DataChecks.Count > 0)
                {
                    var data = responseBody["data"];
                    Assert.IsNotNull(data,
                        $"Fixture '{fixture.Fixture}': expected data checks but response has no 'data' property.");

                    foreach (var check in expectations.DataChecks)
                    {
                        var actual = data[check.Key];
                        Assert.IsNotNull(actual,
                            $"Fixture '{fixture.Fixture}': data check field '{check.Key}' is missing from response.");

                        string expectedValue = check.Value?.ToString();
                        string actualValue = actual.ToString();
                        Assert.AreEqual(expectedValue, actualValue,
                            $"Fixture '{fixture.Fixture}': data check '{check.Key}' expected '{expectedValue}' but got '{actualValue}'.");
                    }
                }

                // Verify error code on failure expectations
                if (!expectations.Success && !string.IsNullOrEmpty(expectations.ErrorCode))
                {
                    var actualErrorCode = responseBody["errorCode"]?.Value<string>();
                    Assert.AreEqual(expectations.ErrorCode, actualErrorCode,
                        $"Fixture '{fixture.Fixture}': expected errorCode '{expectations.ErrorCode}' but got '{actualErrorCode}'.");
                }

                // Track the main tool's created object for cleanup
                if (expectations.Success)
                {
                    var dataToken = responseBody["data"];
                    if (dataToken is JObject dataObj)
                    {
                        // Track by Name (for GameObjects) or Path (for Assets)
                        // Check type before casting — Path may be a JArray (e.g. pathfinding results)
                        var nameToken = dataObj["Name"];
                        var pathToken = dataObj["Path"];
                        string mainCreatedName = null;
                        if (nameToken is JValue nameVal) mainCreatedName = nameVal.Value<string>();
                        if (mainCreatedName == null && pathToken is JValue pathVal) mainCreatedName = pathVal.Value<string>();
                        if (!string.IsNullOrEmpty(mainCreatedName))
                            createdObjects.Add(mainCreatedName);
                    }
                }
            }
            finally
            {
                // 5. Run cleanup
                RunCleanup(fixture.Cleanup, createdObjects);
            }
        }

        // ── Cleanup ──────────────────────────────────────────────────────────

        private void RunCleanup(string cleanupAction, List<string> createdObjects)
        {
            if (string.IsNullOrEmpty(cleanupAction) || createdObjects.Count == 0)
                return;

            switch (cleanupAction)
            {
                case "delete_created_object":
                    // Delete all objects created during setup and the main test, in reverse order
                    for (int i = createdObjects.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            ExecuteTool("mosaic_gameobject_delete",
                                new Dictionary<string, object> { { "name", createdObjects[i] } });
                        }
                        catch (Exception)
                        {
                            // Best-effort cleanup: don't fail the test if cleanup fails
                        }
                    }
                    break;

                case "delete_created_asset":
                    // Delete assets created during the test by path
                    foreach (var name in createdObjects)
                    {
                        try
                        {
                            ExecuteTool("mosaic_asset_delete",
                                new Dictionary<string, object> { { "path", name } });
                        }
                        catch (Exception)
                        {
                            // Best-effort cleanup
                        }
                    }
                    break;
            }
        }

        // ── HTTP helpers ─────────────────────────────────────────────────────

        private ToolResponse ExecuteTool(string toolName, Dictionary<string, object> parameters)
        {
            var body = new JObject(
                new JProperty("tool", toolName),
                new JProperty("parameters", parameters != null
                    ? JObject.FromObject(parameters)
                    : new JObject()));

            string bodyStr = body.ToString(Formatting.None);

            HttpResponseMessage response = DriveUntilComplete(
                () => SendSigned("POST", "/execute", bodyStr));

            return new ToolResponse
            {
                StatusCode = (int)response.StatusCode,
                Body = response.Content.ReadAsStringAsync().Result
            };
        }

        private HttpResponseMessage DriveUntilComplete(Func<Task<HttpResponseMessage>> factory)
        {
            var task = Task.Run(factory);
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            {
                BridgeBootstrap.Dispatcher?.ProcessPendingRequests(maxToProcess: 5);
                Thread.Sleep(20);
            }
            Assert.IsTrue(task.IsCompleted,
                "Request did not complete within 15 seconds — is the bridge running?");
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

            string url = $"http://127.0.0.1:{BridgeBootstrap.Server.Port}{path}";
            var request = new HttpRequestMessage(
                method == "POST" ? HttpMethod.Post : HttpMethod.Get, url);

            if (bodyStr != null)
                request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");

            request.Headers.Add("X-Mosaic-Nonce", nonce);
            request.Headers.Add("X-Mosaic-Timestamp", timestamp);
            request.Headers.Add("X-Mosaic-Signature", signature);

            return _http.SendAsync(request);
        }

        // ── Internal types ───────────────────────────────────────────────────

        private struct ToolResponse
        {
            public int StatusCode;
            public string Body;
        }
    }
}
