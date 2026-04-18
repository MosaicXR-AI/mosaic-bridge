using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Mosaic.Bridge.Core.Security
{
    // ──────────────────────────────────────────────────────────────────────────
    //  Third-party tool integration:
    //
    //  1. Reference com.mosaic.bridge.contracts (Mosaic.Bridge.Contracts.dll)
    //     in your assembly definition's references.
    //  2. Create a static method decorated with
    //     [MosaicTool("vendor/action", "description")]
    //     The method must accept a single typed parameter class and return
    //     ToolResult<T>.
    //  3. Open Edit > Project Settings > Mosaic Bridge, scroll to the
    //     "Allowed Tool Assemblies" section, and add your assembly name
    //     (the Assembly-Definition name, e.g. "com.acme.mosaic-tools").
    //  4. Restart Unity or trigger a domain reload so the bridge re-discovers
    //     tools from the newly allowed assembly.
    //
    //  The AssemblyGuard ensures that only explicitly trusted assemblies can
    //  register tools with the bridge — preventing untrusted packages from
    //  injecting arbitrary MCP-callable operations into your project.
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configurable allowlist of assemblies permitted to register tools with
    /// the Mosaic Bridge. Tools discovered in disallowed assemblies are skipped
    /// with a warning during <see cref="Discovery.ToolRegistry.BuildFromTypeCache"/>.
    /// </summary>
    public static class AssemblyGuard
    {
        /// <summary>EditorPrefs key storing the comma-separated user allowlist.</summary>
        internal const string EditorPrefsKey = "MosaicBridge.AllowedToolAssemblies";

        /// <summary>
        /// Assemblies that are always allowed and cannot be removed via the UI.
        /// </summary>
        private static readonly HashSet<string> DefaultAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Mosaic.Bridge.Tools",
            "Mosaic.Bridge.Tests"
        };

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if <paramref name="assemblyName"/> is on the allowlist
        /// (either a built-in default or a user-added entry).
        /// </summary>
        public static bool IsAllowed(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            if (DefaultAllowed.Contains(assemblyName))
                return true;

            return GetUserEntries().Contains(assemblyName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds an assembly to the user allowlist (persisted in EditorPrefs).
        /// No-op if already present or if it is a built-in default.
        /// </summary>
        public static void Allow(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return;

            assemblyName = assemblyName.Trim();

            if (DefaultAllowed.Contains(assemblyName))
                return;

            var entries = GetUserEntries();
            if (entries.Contains(assemblyName, StringComparer.OrdinalIgnoreCase))
                return;

            entries.Add(assemblyName);
            PersistUserEntries(entries);
        }

        /// <summary>
        /// Removes an assembly from the user allowlist. Built-in defaults
        /// cannot be revoked.
        /// </summary>
        public static void Revoke(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return;

            if (DefaultAllowed.Contains(assemblyName))
                return;

            var entries = GetUserEntries();
            entries.RemoveAll(e => string.Equals(e, assemblyName, StringComparison.OrdinalIgnoreCase));
            PersistUserEntries(entries);
        }

        /// <summary>
        /// Returns all currently allowed assemblies (defaults + user entries),
        /// ordered with defaults first.
        /// </summary>
        public static IReadOnlyList<string> GetAllowedAssemblies()
        {
            var result = new List<string>(DefaultAllowed);
            result.AddRange(GetUserEntries());
            return result;
        }

        /// <summary>
        /// Returns true if the assembly is a built-in default that cannot be removed.
        /// </summary>
        public static bool IsDefault(string assemblyName)
        {
            return !string.IsNullOrEmpty(assemblyName) && DefaultAllowed.Contains(assemblyName);
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        private static List<string> GetUserEntries()
        {
            var raw = EditorPrefs.GetString(EditorPrefsKey, "");
            if (string.IsNullOrEmpty(raw))
                return new List<string>();

            return raw.Split(',')
                      .Select(s => s.Trim())
                      .Where(s => !string.IsNullOrEmpty(s))
                      .ToList();
        }

        private static void PersistUserEntries(List<string> entries)
        {
            EditorPrefs.SetString(EditorPrefsKey, string.Join(",", entries));
        }
    }
}
