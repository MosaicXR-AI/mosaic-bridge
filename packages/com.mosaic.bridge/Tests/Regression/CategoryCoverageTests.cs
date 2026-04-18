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
    /// Verifies that the regression fixture suite has sufficient coverage:
    ///   - Every tool category in the registry has at least one fixture.
    ///   - Every fixture references a tool that actually exists in the registry.
    /// </summary>
    [TestFixture]
    [Explicit("Regression tests require a running bridge — run individually, not with Run All")]
    [Category("Integration")]
    [Category("Regression")]
    public class CategoryCoverageTests
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

        // ── Tests ────────────────────────────────────────────────────────────

        [Test]
        public void AllToolCategories_HaveAtLeastOneFixture()
        {
            Assert.AreEqual(BridgeState.Running, BridgeBootstrap.State,
                "BridgeBootstrap must be Running. Start the Unity bridge before running coverage tests.");

            // 1. Get all tool categories from the live registry
            var registryCategories = GetRegistryCategories();
            Assert.IsTrue(registryCategories.Count > 0,
                "No tool categories found in the registry. Is the bridge running with tools registered?");

            // 2. Get all fixture categories
            var fixtureCategories = GetFixtureCategories();

            // Categories excluded from fixture coverage (dangerous to run in tests)
            var excludedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "build" }; // build/build actually compiles the project (51s+ blocking)

            // 3. Assert every tool category has at least one fixture
            var missingCategories = registryCategories
                .Where(c => !fixtureCategories.Contains(c) && !excludedCategories.Contains(c))
                .OrderBy(c => c)
                .ToList();

            if (missingCategories.Count > 0)
            {
                Assert.Fail(
                    $"The following tool categories have no regression fixtures:\n" +
                    $"  {string.Join(", ", missingCategories)}\n\n" +
                    $"Registry categories ({registryCategories.Count}): {string.Join(", ", registryCategories.OrderBy(c => c))}\n" +
                    $"Fixture categories ({fixtureCategories.Count}): {string.Join(", ", fixtureCategories.OrderBy(c => c))}");
            }
        }

        [Test]
        public void AllFixtures_ReferenceValidTools()
        {
            Assert.AreEqual(BridgeState.Running, BridgeBootstrap.State,
                "BridgeBootstrap must be Running. Start the Unity bridge before running coverage tests.");

            // 1. Get all registered tool names
            var registeredTools = GetRegisteredToolNames();
            Assert.IsTrue(registeredTools.Count > 0,
                "No tools found in the registry.");

            // 2. Load all fixtures and check their tool references
            Assert.IsTrue(Directory.Exists(FixturesPath),
                $"Fixtures directory not found: {FixturesPath}");

            var invalidFixtures = new List<string>();
            string[] files = Directory.GetFiles(FixturesPath, "*.json");

            foreach (string path in files)
            {
                string json = File.ReadAllText(path);
                var fixture = JsonConvert.DeserializeObject<FixtureSchema>(json);
                string fileName = Path.GetFileNameWithoutExtension(path);

                if (fixture?.Tool == null)
                {
                    invalidFixtures.Add($"{fileName}: missing 'tool' field");
                    continue;
                }

                if (!registeredTools.Contains(fixture.Tool))
                {
                    invalidFixtures.Add($"{fileName}: references unknown tool '{fixture.Tool}'");
                }

                // Also check setup steps
                if (fixture.Setup != null)
                {
                    foreach (var step in fixture.Setup)
                    {
                        if (!string.IsNullOrEmpty(step.Tool) && !registeredTools.Contains(step.Tool))
                        {
                            invalidFixtures.Add(
                                $"{fileName}: setup step references unknown tool '{step.Tool}'");
                        }
                    }
                }
            }

            if (invalidFixtures.Count > 0)
            {
                Assert.Fail(
                    $"The following fixtures reference invalid tools:\n" +
                    $"  {string.Join("\n  ", invalidFixtures)}");
            }
        }

        [Test]
        public void AllFixtures_HaveRequiredFields()
        {
            Assert.IsTrue(Directory.Exists(FixturesPath),
                $"Fixtures directory not found: {FixturesPath}");

            var issues = new List<string>();
            string[] files = Directory.GetFiles(FixturesPath, "*.json");
            Assert.IsTrue(files.Length > 0, "No fixture files found.");

            foreach (string path in files)
            {
                string json = File.ReadAllText(path);
                var fixture = JsonConvert.DeserializeObject<FixtureSchema>(json);
                string fileName = Path.GetFileNameWithoutExtension(path);

                if (string.IsNullOrEmpty(fixture?.Fixture))
                    issues.Add($"{fileName}: missing 'fixture' identifier");
                if (string.IsNullOrEmpty(fixture?.Tool))
                    issues.Add($"{fileName}: missing 'tool' name");
                if (string.IsNullOrEmpty(fixture?.Category))
                    issues.Add($"{fileName}: missing 'category'");
                if (string.IsNullOrEmpty(fixture?.Description))
                    issues.Add($"{fileName}: missing 'description'");
                if (fixture?.Expectations == null)
                    issues.Add($"{fileName}: missing 'expectations' block");
            }

            if (issues.Count > 0)
            {
                Assert.Fail(
                    $"Fixture schema validation failures:\n  {string.Join("\n  ", issues)}");
            }
        }

        [Test]
        public void FixtureIdentifiers_AreUnique()
        {
            Assert.IsTrue(Directory.Exists(FixturesPath),
                $"Fixtures directory not found: {FixturesPath}");

            string[] files = Directory.GetFiles(FixturesPath, "*.json");
            var seen = new Dictionary<string, string>();
            var duplicates = new List<string>();

            foreach (string path in files)
            {
                string json = File.ReadAllText(path);
                var fixture = JsonConvert.DeserializeObject<FixtureSchema>(json);
                string fileName = Path.GetFileName(path);

                if (fixture?.Fixture == null) continue;

                if (seen.TryGetValue(fixture.Fixture, out string existingFile))
                {
                    duplicates.Add($"'{fixture.Fixture}' appears in both {existingFile} and {fileName}");
                }
                else
                {
                    seen[fixture.Fixture] = fileName;
                }
            }

            if (duplicates.Count > 0)
            {
                Assert.Fail($"Duplicate fixture identifiers:\n  {string.Join("\n  ", duplicates)}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private HashSet<string> GetRegistryCategories()
        {
            var toolsJson = FetchToolsList();
            var tools = toolsJson["tools"] as JArray;
            if (tools == null) return new HashSet<string>();

            return new HashSet<string>(
                tools.Select(t => t["category"]?.Value<string>())
                     .Where(c => !string.IsNullOrEmpty(c)),
                StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<string> GetRegisteredToolNames()
        {
            var toolsJson = FetchToolsList();
            var tools = toolsJson["tools"] as JArray;
            if (tools == null) return new HashSet<string>();

            return new HashSet<string>(
                tools.Select(t => t["name"]?.Value<string>())
                     .Where(n => !string.IsNullOrEmpty(n)),
                StringComparer.Ordinal);
        }

        private HashSet<string> GetFixtureCategories()
        {
            if (!Directory.Exists(FixturesPath))
                return new HashSet<string>();

            var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] files = Directory.GetFiles(FixturesPath, "*.json");

            foreach (string path in files)
            {
                string json = File.ReadAllText(path);
                var fixture = JsonConvert.DeserializeObject<FixtureSchema>(json);
                if (!string.IsNullOrEmpty(fixture?.Category))
                    categories.Add(fixture.Category);
            }

            return categories;
        }

        private JObject FetchToolsList()
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
            {
                var response = DriveUntilComplete(() => SendSignedGet(http, "/tools"));
                string body = response.Content.ReadAsStringAsync().Result;
                return JObject.Parse(body);
            }
        }

        private HttpResponseMessage DriveUntilComplete(Func<Task<HttpResponseMessage>> factory)
        {
            var task = Task.Run(factory);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            {
                BridgeBootstrap.Dispatcher?.ProcessPendingRequests(maxToProcess: 5);
                Thread.Sleep(20);
            }
            Assert.IsTrue(task.IsCompleted,
                "Request did not complete within 10 seconds — is the bridge running?");
            return task.GetAwaiter().GetResult();
        }

        private static Task<HttpResponseMessage> SendSignedGet(HttpClient http, string path)
        {
            string nonce = Guid.NewGuid().ToString("N");
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string bodySha256 = HmacCanonicalizer.ComputeBodySha256(Array.Empty<byte>());
            string canonical = HmacCanonicalizer.Canonicalize(
                nonce, timestamp, "GET", path, bodySha256);

            byte[] sigBytes;
            using (var hmac = new HMACSHA256(BridgeBootstrap.Secret))
                sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));

            string signature = BitConverter.ToString(sigBytes).Replace("-", "").ToLowerInvariant();

            string url = $"http://127.0.0.1:{BridgeBootstrap.Server.Port}{path}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Mosaic-Nonce", nonce);
            request.Headers.Add("X-Mosaic-Timestamp", timestamp);
            request.Headers.Add("X-Mosaic-Signature", signature);

            return http.SendAsync(request);
        }
    }
}
