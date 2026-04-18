using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Runtime;
using UnityEditor;

namespace Mosaic.Bridge.Core.Mcp
{
    /// <summary>
    /// Manages the MCP server as a child process of the Unity bridge.
    /// Handles spawn, crash respawn with circuit breaker, and graceful shutdown.
    /// The MCP server survives domain reloads — only killed on editor quit.
    /// </summary>
    public class McpServerProcess
    {
        private const int CrashWindowMs = 60_000;
        private const int MaxCrashesInWindow = 3;
        private const int GracefulShutdownMs = 5000;

        private readonly IProcessLauncher _launcher;
        private readonly IMosaicLogger _logger;
        private readonly Func<long> _nowProvider;

        private readonly object _lock = new object();
        private readonly List<long> _crashTimestamps = new List<long>();

        private IProcessHandle _handle;
        private bool _circuitBroken;
        private volatile bool _shutdownRequested;
        private string _lastDiscoveryFilePath;

        public bool IsRunning
        {
            get
            {
                lock (_lock)
                {
                    return _handle != null && !_handle.HasExited && !_circuitBroken;
                }
            }
        }

        public int CurrentPid
        {
            get
            {
                lock (_lock)
                {
                    return _handle != null && !_handle.HasExited ? _handle.Id : 0;
                }
            }
        }

        public McpServerProcess(IProcessLauncher launcher, IMosaicLogger logger, Func<long> nowProvider = null)
        {
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <summary>
        /// Spawns the MCP server child process, or reuses an existing one if the PID
        /// in the discovery file is still alive.
        /// </summary>
        /// <returns>The PID of the managed process, or 0 on failure.</returns>
        public int Start(string discoveryFilePath)
        {
            lock (_lock)
            {
                _lastDiscoveryFilePath = discoveryFilePath;

                // Check for existing process via discovery file
                var existingPid = ReadExistingMcpPid();
                if (existingPid > 0 && _launcher.IsProcessAlive(existingPid))
                {
                    _logger.Info("Reusing existing MCP server process",
                        ("pid", (object)existingPid));
                    return existingPid;
                }

                // Build ProcessStartInfo
                var customPath = EditorPrefs.GetString("Mosaic.Bridge.McpServerPath", "");
                var psi = new ProcessStartInfo();

                if (!string.IsNullOrEmpty(customPath))
                {
                    psi.FileName = "node";
                    psi.Arguments = $"\"{customPath}\" --discovery \"{discoveryFilePath}\"";
                    _logger.Info("MCP server using custom path", ("path", (object)customPath));
                }
                else if (System.IO.File.Exists(System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? "",
                    "node_modules", "@mosaic", "mcp-server", "dist", "index.js")))
                {
                    // Local npm install found
                    var localPath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath),
                        "node_modules", "@mosaic", "mcp-server", "dist", "index.js");
                    psi.FileName = "node";
                    psi.Arguments = $"\"{localPath}\" --discovery \"{discoveryFilePath}\"";
                    _logger.Info("MCP server using local install", ("path", (object)localPath));
                }
                else if (_launcher is SystemProcessLauncher)
                {
                    // Production path: npm package not available — skip spawn silently
                    _logger.Debug("MCP server not spawned: @mosaic/mcp-server not installed. " +
                        "Set Mosaic.Bridge.McpServerPath in EditorPrefs or install the npm package.");
                    return 0;
                }
                else
                {
                    // Test/custom launcher path: use npx as fallback
                    psi.FileName = "npx";
                    psi.Arguments = $"@mosaic/mcp-server --discovery \"{discoveryFilePath}\"";
                }

                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                try
                {
                    _handle = _launcher.Start(psi);
                    _handle.EnableRaisingEvents = true;
                    _handle.Exited += OnProcessExited;

                    var pid = _handle.Id;
                    _logger.Info("MCP server process started", ("pid", (object)pid));
                    return pid;
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to start MCP server process", ex);
                    return 0;
                }
            }
        }

        /// <summary>
        /// Graceful shutdown: waits up to 5 seconds, then force-kills.
        /// Sets <see cref="_shutdownRequested"/> to prevent respawn on Exited event.
        /// </summary>
        public void Stop()
        {
            _shutdownRequested = true;

            lock (_lock)
            {
                if (_handle == null)
                    return;

                try
                {
                    if (_handle.HasExited)
                    {
                        _logger.Info("MCP server process already exited");
                        return;
                    }
                }
                catch (InvalidOperationException)
                {
                    return;
                }

                var pid = _handle.Id;

                try
                {
                    if (!_handle.WaitForExit(GracefulShutdownMs))
                    {
                        _handle.Kill();
                        _logger.Warn("MCP server force-killed after timeout", ("pid", (object)pid));
                    }
                    else
                    {
                        _logger.Info("MCP server exited gracefully", ("pid", (object)pid));
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process already exited between our check and Kill() call
                }
                finally
                {
                    _handle.Dispose();
                    _handle = null;
                }
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            if (_shutdownRequested)
                return;

            lock (_lock)
            {
                var now = _nowProvider();
                _crashTimestamps.Add(now);

                // Trim timestamps older than the crash window
                var cutoff = now - CrashWindowMs;
                _crashTimestamps.RemoveAll(t => t < cutoff);

                if (_crashTimestamps.Count >= MaxCrashesInWindow)
                {
                    _circuitBroken = true;
                    _logger.Error("MCP server circuit breaker tripped: too many crashes in window",
                        null,
                        ("crashCount", (object)_crashTimestamps.Count),
                        ("windowMs", (object)CrashWindowMs));
                    return;
                }

                _logger.Warn("MCP server process crashed, respawning",
                    ("crashCount", (object)_crashTimestamps.Count));

                // Clear handle before respawn
                _handle?.Dispose();
                _handle = null;
            }

            // Respawn outside of lock to avoid re-entrancy issues
            Start(_lastDiscoveryFilePath);
        }

        private int ReadExistingMcpPid()
        {
            try
            {
                var data = DiscoveryFile.Read(_logger);
                return data?.McpServerPid ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
