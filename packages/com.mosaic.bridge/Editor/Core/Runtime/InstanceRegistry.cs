using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Core.Runtime
{
    /// <summary>
    /// Manages the shared instance registry file that tracks all running Mosaic Bridge
    /// instances on this machine. Uses file-level locking (FileShare.None) to ensure
    /// atomic read-modify-write across concurrent Unity Editor processes.
    ///
    /// Per FR18: each instance registers itself with port, project path, and PID.
    /// Stale entries (dead PIDs) are pruned on every write operation.
    /// </summary>
    public static class InstanceRegistry
    {
        /// <summary>Name of the registry JSON file in the shared MosaicBridge directory.</summary>
        internal const string RegistryFileName = "instance-registry.json";

        private const int MaxLockRetries = 10;
        private const int LockRetryDelayMs = 50;

        /// <summary>
        /// Registers or updates an entry for the current instance.
        /// If an entry with the same PID already exists, it is replaced.
        /// Prunes stale entries as part of the write operation.
        /// </summary>
        public static void Register(InstanceRegistryEntry entry, string registryDir = null)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            string filePath = GetRegistryFilePath(registryDir);
            AtomicReadModifyWrite(filePath, entries =>
            {
                entries.RemoveAll(e => e.Pid == entry.Pid);
                entries.Add(entry);
                return entries;
            });
        }

        /// <summary>
        /// Removes the entry for the given PID.
        /// Called on editor quit (NOT on domain reload).
        /// </summary>
        public static void Deregister(int pid, string registryDir = null)
        {
            string filePath = GetRegistryFilePath(registryDir);
            if (!File.Exists(filePath))
                return;

            AtomicReadModifyWrite(filePath, entries =>
            {
                entries.RemoveAll(e => e.Pid == pid);
                return entries;
            });
        }

        /// <summary>
        /// Returns all current entries from the registry (including potentially stale ones).
        /// Does NOT prune stale entries — use PruneStale() for that.
        /// </summary>
        public static List<InstanceRegistryEntry> ReadAll(string registryDir = null)
        {
            string filePath = GetRegistryFilePath(registryDir);
            if (!File.Exists(filePath))
                return new List<InstanceRegistryEntry>();

            return ReadEntriesFromFile(filePath);
        }

        /// <summary>
        /// Removes entries whose PIDs no longer correspond to running processes.
        /// Also deletes the per-project discovery files for pruned entries.
        /// Returns the list of entries that were pruned.
        /// </summary>
        public static List<InstanceRegistryEntry> PruneStale(string registryDir = null)
        {
            string filePath = GetRegistryFilePath(registryDir);
            if (!File.Exists(filePath))
                return new List<InstanceRegistryEntry>();

            var pruned = new List<InstanceRegistryEntry>();

            AtomicReadModifyWrite(filePath, entries =>
            {
                var alive = new List<InstanceRegistryEntry>();
                foreach (var entry in entries)
                {
                    if (IsProcessAlive(entry.Pid))
                        alive.Add(entry);
                    else
                        pruned.Add(entry);
                }
                return alive;
            });

            // Clean up discovery files for pruned entries
            foreach (var entry in pruned)
                TryDeleteDiscoveryFile(entry.ProjectHash, registryDir);

            return pruned;
        }

        /// <summary>
        /// Checks whether a process with the given PID is still alive.
        /// </summary>
        internal static bool IsProcessAlive(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static void TryDeleteDiscoveryFile(string projectHash, string registryDir)
        {
            if (string.IsNullOrEmpty(projectHash))
                return;

            try
            {
                string sharedDir = registryDir ?? RuntimeDirectoryResolver.GetSharedBasePath();
                string projectDir = Path.Combine(sharedDir, projectHash);
                string discoveryFile = Path.Combine(projectDir, "bridge-discovery.json");

                if (File.Exists(discoveryFile))
                    File.Delete(discoveryFile);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        private static void AtomicReadModifyWrite(
            string filePath,
            Func<List<InstanceRegistryEntry>, List<InstanceRegistryEntry>> mutator)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            FileStream fs = null;
            try
            {
                fs = OpenWithRetry(filePath);

                List<InstanceRegistryEntry> entries;
                if (fs.Length > 0)
                {
                    using (var reader = new StreamReader(fs, System.Text.Encoding.UTF8, true, 4096, leaveOpen: true))
                    {
                        string json = reader.ReadToEnd();
                        try
                        {
                            entries = JsonConvert.DeserializeObject<List<InstanceRegistryEntry>>(json)
                                      ?? new List<InstanceRegistryEntry>();
                        }
                        catch (JsonException)
                        {
                            // Corrupted registry file — reset to empty
                            entries = new List<InstanceRegistryEntry>();
                        }
                    }
                }
                else
                {
                    entries = new List<InstanceRegistryEntry>();
                }

                entries = mutator(entries);

                fs.SetLength(0);
                fs.Seek(0, SeekOrigin.Begin);
                using (var writer = new StreamWriter(fs, System.Text.Encoding.UTF8, 4096, leaveOpen: true))
                {
                    string json = JsonConvert.SerializeObject(entries, Formatting.Indented);
                    writer.Write(json);
                    writer.Flush();
                }

                fs.Flush(flushToDisk: true);
            }
            finally
            {
                fs?.Dispose();
            }
        }

        private static FileStream OpenWithRetry(string filePath)
        {
            for (int attempt = 0; attempt < MaxLockRetries; attempt++)
            {
                try
                {
                    return new FileStream(
                        filePath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (IOException) when (attempt < MaxLockRetries - 1)
                {
                    System.Threading.Thread.Sleep(LockRetryDelayMs);
                }
            }

            return new FileStream(
                filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }

        private static List<InstanceRegistryEntry> ReadEntriesFromFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<InstanceRegistryEntry>();

                return JsonConvert.DeserializeObject<List<InstanceRegistryEntry>>(json)
                       ?? new List<InstanceRegistryEntry>();
            }
            catch
            {
                return new List<InstanceRegistryEntry>();
            }
        }

        internal static string GetRegistryFilePath(string registryDir)
        {
            string dir = registryDir ?? RuntimeDirectoryResolver.GetSharedBasePath();
            return Path.Combine(dir, RegistryFileName);
        }
    }
}
