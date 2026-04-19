using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Mosaic.Bridge.Contracts.Interfaces;

namespace Mosaic.Bridge.Core.Runtime
{
    /// <summary>
    /// Returns the per-user runtime directory for Mosaic Bridge, cross-platform.
    /// Creates the directory on first call and sets Unix permissions to 0700.
    /// </summary>
    public static class RuntimeDirectoryResolver
    {
        /// <summary>
        /// Returns the absolute runtime directory path, creating it if it does not exist.
        /// On Unix, sets directory permissions to 0700.
        /// </summary>
        public static string Resolve()
        {
            return Resolve(null);
        }

        /// <summary>
        /// Returns the absolute runtime directory path, creating it if it does not exist.
        /// On Unix, sets directory permissions to 0700. Chmod failures are logged and non-fatal.
        /// </summary>
        public static string Resolve(IMosaicLogger logger)
        {
            string path;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Mosaic", "Bridge");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = Path.Combine(home, "Library", "Application Support", "Mosaic", "Bridge");
            }
            else
            {
                // Linux: prefer XDG_RUNTIME_DIR if set and directory exists
                var xdgRuntime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
                if (!string.IsNullOrEmpty(xdgRuntime) && Directory.Exists(xdgRuntime))
                {
                    path = Path.Combine(xdgRuntime, "mosaic-bridge");
                }
                else
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    path = Path.Combine(home, ".cache", "mosaic-bridge");
                }
            }

            Directory.CreateDirectory(path);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ChmodDirectory(path, logger);
            }

            return path;
        }

        /// <summary>
        /// Returns the shared base path (parent of per-project directories).
        /// Used by InstanceRegistry to store the machine-wide instance-registry.json, which is
        /// the only file intentionally shared across concurrent Unity Editor instances.
        /// </summary>
        public static string GetSharedBasePath()
        {
            return Resolve();
        }

        /// <summary>
        /// Returns a 16-hex-char project hash derived from Application.dataPath. Used to scope
        /// per-project runtime state (discovery file, status file, logs) when multiple Unity
        /// Editors are running simultaneously.
        /// </summary>
        public static string GetProjectHash()
        {
            var dataPath = UnityEngine.Application.dataPath;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(dataPath);
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Returns the project-scoped runtime directory (shared base + project hash) and
        /// creates it if it does not exist. All per-instance state (discovery file, status,
        /// logs) must live here so that concurrent Unity Editors on different projects do
        /// not overwrite each other's files.
        /// </summary>
        public static string ResolveProject(IMosaicLogger logger)
        {
            var projectDir = Path.Combine(Resolve(logger), GetProjectHash());
            if (!Directory.Exists(projectDir))
            {
                Directory.CreateDirectory(projectDir);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ChmodDirectory(projectDir, logger);
                }
            }
            return projectDir;
        }

        /// <summary>Parameterless variant of <see cref="ResolveProject(IMosaicLogger)"/>.</summary>
        public static string ResolveProject()
        {
            return ResolveProject(null);
        }

        /// <summary>
        /// Returns the full path to the bridge discovery file for the current project.
        /// Different Unity Editors produce different paths because each hashes its own
        /// Application.dataPath.
        /// </summary>
        public static string GetDiscoveryFilePath()
        {
            return Path.Combine(ResolveProject(), "bridge-discovery.json");
        }

        /// <summary>
        /// Returns the full path to the log directory for the current project, creating it
        /// if it does not exist.
        /// </summary>
        public static string GetLogDirectoryPath()
        {
            var logDir = Path.Combine(ResolveProject(), "logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
            return logDir;
        }

        private static void ChmodDirectory(string path, IMosaicLogger logger)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"700 \"{path}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        logger?.Warn("chmod 700 failed on runtime directory",
                            ("path", path),
                            ("exitCode", (object)proc.ExitCode));
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Warn("chmod 700 threw an exception for runtime directory",
                    ("path", path),
                    ("exception", (object)ex.Message));
            }
        }
    }
}
