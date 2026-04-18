using System;
using System.Collections.Generic;
using System.Reflection;

namespace Mosaic.Bridge.Tools.Reflection
{
    /// <summary>
    /// Centralized security blocklist for reflection-based tool calls.
    /// Prevents invocation of dangerous methods that could compromise the host system.
    /// </summary>
    public static class ReflectionBlocklist
    {
        /// <summary>
        /// Fully-qualified "Type.Method" signatures that are unconditionally blocked.
        /// </summary>
        private static readonly HashSet<string> BlockedMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Diagnostics.Process.Start",
            "System.Diagnostics.Process.Kill",
            "System.IO.File.Delete",
            "System.IO.Directory.Delete",
            "System.Environment.Exit",
            "System.Reflection.Assembly.Load",
            "System.Reflection.Assembly.LoadFrom",
            "UnityEngine.Application.Quit",
        };

        /// <summary>
        /// Entire namespaces / types whose methods are all blocked.
        /// </summary>
        private static readonly HashSet<string> BlockedDeclaringTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Runtime.InteropServices.Marshal",
        };

        /// <summary>
        /// Returns true if the given method is on the blocklist.
        /// </summary>
        public static bool IsBlocked(MethodInfo method)
        {
            if (method == null) return false;

            string declaringType = method.DeclaringType?.FullName;
            if (declaringType == null) return false;

            // Block entire declaring type
            if (BlockedDeclaringTypes.Contains(declaringType))
                return true;

            // Block specific method signatures
            string fullSignature = $"{declaringType}.{method.Name}";
            return BlockedMethods.Contains(fullSignature);
        }

        /// <summary>
        /// Returns a human-readable reason if the method is blocked, or null if allowed.
        /// </summary>
        public static string GetBlockReason(MethodInfo method)
        {
            if (method == null) return null;

            string declaringType = method.DeclaringType?.FullName;
            if (declaringType == null) return null;

            if (BlockedDeclaringTypes.Contains(declaringType))
                return $"All methods on type '{declaringType}' are blocked for security reasons.";

            string fullSignature = $"{declaringType}.{method.Name}";
            if (BlockedMethods.Contains(fullSignature))
                return $"Method '{fullSignature}' is blocked for security reasons.";

            return null;
        }
    }
}
