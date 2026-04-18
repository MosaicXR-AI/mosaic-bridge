using System;
using System.Collections.Generic;
using System.IO;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Runtime;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Runtime
{
    [TestFixture]
    public class DiscoveryFileTests
    {
        private string _discoveryPath;
        private string _tmpPath;

        [SetUp]
        public void SetUp()
        {
            _discoveryPath = RuntimeDirectoryResolver.GetDiscoveryFilePath();
            _tmpPath = _discoveryPath + ".tmp";
        }

        [TearDown]
        public void TearDown()
        {
            // NOTE: Do NOT delete _discoveryPath here.
            // The discovery file is the LIVE file used by the running bridge.
            // Deleting it breaks the MCP server connection.
            // Tests that write to this path are integration tests that accept
            // the side effect of overwriting the live file during the test run.
            if (File.Exists(_tmpPath)) File.Delete(_tmpPath);
        }

        // ── Write + Read round-trip ─────────────────────────────────────────────

        [Test]
        public void Write_ThenRead_RoundTripsAllFields()
        {
            var logger = new RecordingLogger();
            var data = MakeValidData();

            DiscoveryFile.Write(data, new byte[32], logger);
            var result = DiscoveryFile.Read(logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(data.SchemaVersion, result.SchemaVersion);
            Assert.AreEqual(data.Port, result.Port);
            Assert.AreEqual(data.ProcessId, result.ProcessId);
            Assert.AreEqual(data.StartedUnixSeconds, result.StartedUnixSeconds);
            Assert.AreEqual(data.SecretBase64, result.SecretBase64);
            Assert.AreEqual(data.UnityProjectPath, result.UnityProjectPath);
            Assert.AreEqual(data.UnityVersion, result.UnityVersion);
        }

        // ── Write validation ────────────────────────────────────────────────────

        [Test]
        public void Write_PortZero_ThrowsInvalidOperationException()
        {
            var data = MakeValidData();
            data.Port = 0;

            Assert.Throws<InvalidOperationException>(() => DiscoveryFile.Write(data, null, new RecordingLogger()));
        }

        [Test]
        public void Write_Port65536_ThrowsInvalidOperationException()
        {
            var data = MakeValidData();
            data.Port = 65536;

            Assert.Throws<InvalidOperationException>(() => DiscoveryFile.Write(data, null, new RecordingLogger()));
        }

        [Test]
        public void Write_EmptySecretBase64_ThrowsInvalidOperationException()
        {
            var data = MakeValidData();
            data.SecretBase64 = string.Empty;

            Assert.Throws<InvalidOperationException>(() => DiscoveryFile.Write(data, null, new RecordingLogger()));
        }

        [Test]
        public void Write_WrongSchemaVersion_ThrowsInvalidOperationException()
        {
            var data = MakeValidData();
            data.SchemaVersion = "2.0";

            Assert.Throws<InvalidOperationException>(() => DiscoveryFile.Write(data, null, new RecordingLogger()));
        }

        // ── Read edge cases ─────────────────────────────────────────────────────

        [Test]
        public void Read_FileDoesNotExist_ReturnsNull()
        {
            if (File.Exists(_discoveryPath)) File.Delete(_discoveryPath);

            var result = DiscoveryFile.Read(new RecordingLogger());

            Assert.IsNull(result);
        }

        [Test]
        public void Read_UnknownSchemaVersion_ReturnsNullAndLogsWarning()
        {
            var logger = new RecordingLogger();
            var json = "{ \"schema_version\": \"2.0\", \"port\": 8080, \"secret_base64\": \"abc\" }";
            File.WriteAllText(_discoveryPath, json);

            var result = DiscoveryFile.Read(logger);

            Assert.IsNull(result);
            Assert.IsTrue(logger.Warnings.Count > 0, "Expected at least one warning to be logged");
        }

        [Test]
        public void Read_MalformedJson_ReturnsNullAndLogsWarning()
        {
            var logger = new RecordingLogger();
            File.WriteAllText(_discoveryPath, "{ this is not valid json !!!");

            var result = DiscoveryFile.Read(logger);

            Assert.IsNull(result);
            Assert.IsTrue(logger.Warnings.Count > 0, "Expected at least one warning to be logged");
        }

        // ── Delete ──────────────────────────────────────────────────────────────

        [Test]
        public void Delete_FileDoesNotExist_DoesNotThrow()
        {
            if (File.Exists(_discoveryPath)) File.Delete(_discoveryPath);

            Assert.DoesNotThrow(() => DiscoveryFile.Delete(new RecordingLogger()));
        }

        // ── Atomic write ────────────────────────────────────────────────────────

        [Test]
        public void Write_AfterSuccess_TmpFileIsGone()
        {
            DiscoveryFile.Write(MakeValidData(), new byte[32], new RecordingLogger());

            Assert.IsFalse(File.Exists(_tmpPath), $"Tmp file should not exist after successful write: {_tmpPath}");
            Assert.IsTrue(File.Exists(_discoveryPath), $"Final file should exist after write: {_discoveryPath}");
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static DiscoveryFileData MakeValidData() => new DiscoveryFileData
        {
            SchemaVersion = DiscoveryFileData.CurrentSchemaVersion,
            Port = 49152,
            ProcessId = 12345,
            StartedUnixSeconds = 1_700_000_000L,
            SecretBase64 = Convert.ToBase64String(new byte[32]),
            UnityProjectPath = "/Users/test/MyProject",
            UnityVersion = "6000.0.0f1"
        };

        // ── RecordingLogger ─────────────────────────────────────────────────────

        private sealed class RecordingLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
            public List<string> Messages { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();

            public bool IsEnabled(LogLevel level) => level >= MinimumLevel;

            public void Trace(string message, params (string Key, object Value)[] context)
                => Messages.Add($"TRACE: {message}");

            public void Debug(string message, params (string Key, object Value)[] context)
                => Messages.Add($"DEBUG: {message}");

            public void Info(string message, params (string Key, object Value)[] context)
                => Messages.Add($"INFO: {message}");

            public void Warn(string message, params (string Key, object Value)[] context)
            {
                Messages.Add($"WARN: {message}");
                Warnings.Add(message);
            }

            public void Error(string message, Exception exception = null, params (string Key, object Value)[] context)
                => Messages.Add($"ERROR: {message}");
        }
    }
}
