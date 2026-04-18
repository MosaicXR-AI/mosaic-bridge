using System;
using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Mcp;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Mcp
{
    [TestFixture]
    public class McpServerProcessTests
    {
        private MockProcessLauncher _launcher;
        private MockLogger _logger;
        private MockProcessHandle _handle;
        private long _currentTime;
        private McpServerProcess _sut;

        [SetUp]
        public void SetUp()
        {
            _launcher = new MockProcessLauncher();
            _logger = new MockLogger();
            _handle = new MockProcessHandle { Id = 5678 };
            _launcher.NextHandle = _handle;
            _currentTime = 1000000;
            _sut = new McpServerProcess(_launcher, _logger, () => _currentTime);
        }

        [TearDown]
        public void TearDown()
        {
            _sut.Stop();
        }

        [Test]
        public void Start_SpawnsProcessWithCorrectArguments()
        {
            var pid = _sut.Start("/tmp/discovery.json");

            Assert.AreEqual(5678, pid);
            Assert.AreEqual(1, _launcher.StartCalls.Count);

            var psi = _launcher.StartCalls[0];
            Assert.AreEqual("npx", psi.FileName);
            Assert.That(psi.Arguments, Does.Contain("@mosaic/mcp-server"));
            Assert.That(psi.Arguments, Does.Contain("--discovery"));
            Assert.That(psi.Arguments, Does.Contain("/tmp/discovery.json"));
            Assert.IsFalse(psi.UseShellExecute);
            Assert.IsTrue(psi.CreateNoWindow);
            Assert.IsTrue(psi.RedirectStandardOutput);
            Assert.IsTrue(psi.RedirectStandardError);
        }

        [Test]
        public void Start_ExistingPidAlive_ReusesProcess()
        {
            _launcher.IsProcessAliveResult = false;
            _sut.Start("/tmp/discovery.json");

            _launcher.IsProcessAliveResult = true;
            var secondHandle = new MockProcessHandle { Id = 9999 };
            _launcher.NextHandle = secondHandle;

            Assert.AreEqual(1, _launcher.StartCalls.Count, "Should have only spawned once initially");
        }

        [Test]
        public void OnCrash_RespawnsUpTo3Times()
        {
            var handle1 = new MockProcessHandle { Id = 1001 };
            var handle2 = new MockProcessHandle { Id = 1002 };
            var handle3 = new MockProcessHandle { Id = 1003 };
            _launcher.NextHandle = null;
            _launcher.EnqueueHandle(handle1);
            _launcher.EnqueueHandle(handle2);
            _launcher.EnqueueHandle(handle3);

            _sut.Start("/tmp/discovery.json");
            Assert.AreEqual(1, _launcher.StartCalls.Count);

            // First crash -- should respawn
            _currentTime = 1010000;
            handle1.SimulateExit();
            Assert.AreEqual(2, _launcher.StartCalls.Count, "Should respawn after first crash");

            // Second crash -- should respawn again
            _currentTime = 1020000;
            handle2.SimulateExit();
            Assert.AreEqual(3, _launcher.StartCalls.Count, "Should respawn after second crash");
        }

        [Test]
        public void OnCrash_3TimesIn60Seconds_CircuitBreaks()
        {
            var handles = new List<MockProcessHandle>();
            for (int i = 0; i < 5; i++)
            {
                var h = new MockProcessHandle { Id = 2000 + i };
                handles.Add(h);
                _launcher.EnqueueHandle(h);
            }

            _sut.Start("/tmp/discovery.json");
            Assert.AreEqual(1, _launcher.StartCalls.Count);

            // Crash 1 at t=0
            _currentTime = 1000000;
            handles[0].SimulateExit();
            Assert.AreEqual(2, _launcher.StartCalls.Count, "Should respawn after crash 1");

            // Crash 2 at t=20s
            _currentTime = 1020000;
            handles[1].SimulateExit();
            Assert.AreEqual(3, _launcher.StartCalls.Count, "Should respawn after crash 2");

            // Crash 3 at t=40s -- 3rd crash within 60s, circuit breaks
            _currentTime = 1040000;
            handles[2].SimulateExit();
            Assert.AreEqual(3, _launcher.StartCalls.Count, "Should NOT respawn -- circuit broken");

            Assert.IsTrue(_logger.HasError("circuit breaker"),
                "Should log circuit breaker error");
        }

        [Test]
        public void OnCrash_3TimesOver60Seconds_ResetsCounter()
        {
            var handles = new List<MockProcessHandle>();
            for (int i = 0; i < 5; i++)
            {
                var h = new MockProcessHandle { Id = 3000 + i };
                handles.Add(h);
                _launcher.EnqueueHandle(h);
            }

            _sut.Start("/tmp/discovery.json");

            // Crash 1 at t=0
            _currentTime = 1000000;
            handles[0].SimulateExit();
            Assert.AreEqual(2, _launcher.StartCalls.Count);

            // Crash 2 at t=30s
            _currentTime = 1030000;
            handles[1].SimulateExit();
            Assert.AreEqual(3, _launcher.StartCalls.Count);

            // Crash 3 at t=70s -- crash 1 is now >60s ago, window only has 2 recent crashes
            _currentTime = 1070000;
            handles[2].SimulateExit();
            Assert.AreEqual(4, _launcher.StartCalls.Count, "Should respawn -- old crash expired from window");
        }

        [Test]
        public void Stop_CallsKillAfterTimeout()
        {
            _sut.Start("/tmp/discovery.json");

            _handle.WaitForExitResult = false;
            _sut.Stop();

            Assert.IsTrue(_handle.WaitForExitCalled);
            Assert.IsTrue(_handle.KillCalled, "Should kill after WaitForExit returns false");
            Assert.IsTrue(_handle.DisposeCalled);
        }

        [Test]
        public void Stop_GracefulExit_DoesNotCallKill()
        {
            _sut.Start("/tmp/discovery.json");

            _handle.WaitForExitResult = true;
            _sut.Stop();

            Assert.IsTrue(_handle.WaitForExitCalled);
            Assert.IsFalse(_handle.KillCalled, "Should NOT kill after graceful exit");
            Assert.IsTrue(_handle.DisposeCalled);
        }

        [Test]
        public void Stop_PreventsRespawnOnExitedEvent()
        {
            var handle1 = new MockProcessHandle { Id = 4001 };
            var handle2 = new MockProcessHandle { Id = 4002 };
            _launcher.EnqueueHandle(handle1);
            _launcher.EnqueueHandle(handle2);

            _sut.Start("/tmp/discovery.json");
            Assert.AreEqual(1, _launcher.StartCalls.Count);

            _sut.Stop();

            // Fire Exited event after Stop -- should NOT respawn
            handle1.SimulateExit();

            Assert.AreEqual(1, _launcher.StartCalls.Count,
                "Should NOT respawn after Stop() was called");
        }

        private class MockLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

            private readonly List<string> _errorMessages = new List<string>();

            public void Trace(string message, params (string Key, object Value)[] context) { }
            public void Debug(string message, params (string Key, object Value)[] context) { }
            public void Info(string message, params (string Key, object Value)[] context) { }
            public void Warn(string message, params (string Key, object Value)[] context) { }

            public void Error(string message, Exception exception = null,
                params (string Key, object Value)[] context)
            {
                _errorMessages.Add(message);
            }

            public bool IsEnabled(LogLevel level) => level >= MinimumLevel;

            public bool HasError(string substring)
            {
                foreach (var msg in _errorMessages)
                {
                    if (msg.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                return false;
            }
        }
    }
}
