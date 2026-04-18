using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mosaic.Bridge.Core.Security
{
    /// <summary>
    /// Static utility that redacts sensitive patterns (license keys, tokens, secrets)
    /// from log messages. Uses an allowlist of safe field names that pass through unmodified.
    /// Story 8.4 — Allowlist-Based Log Redaction.
    /// </summary>
    public static class LogRedactor
    {
        private static readonly (Regex Regex, string Replacement)[] RedactionRules;

        /// <summary>
        /// Field names that are safe to log without redaction.
        /// Values for these context keys pass through unmodified.
        /// </summary>
        private static readonly HashSet<string> SafeFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "toolName", "tool", "path", "gameObjectName", "componentType",
            "category", "key", "port", "state", "status", "count",
            "instanceId", "sceneName", "assetPath", "executionMode"
        };

        static LogRedactor()
        {
            var rules = new (string Pattern, string Replacement)[]
            {
                // License keys (format: MOSAIC-XXXX-XXXX-XXXX)
                (@"MOSAIC-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}", "MOSAIC-****-****-****"),
                // Bearer tokens
                (@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", "Bearer [REDACTED]"),
                // API keys, secrets, tokens, passwords in key=value or key:value form
                (@"(?i)(api[_-]?key|secret|token|password)\s*[=:]\s*\S+", "$1=[REDACTED]"),
                // Base64 secrets (32+ chars of base64) — checked last to avoid false positives
                (@"(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{32,}={0,2}(?![A-Za-z0-9+/=])", "[REDACTED_SECRET]"),
            };

            RedactionRules = new (Regex, string)[rules.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                RedactionRules[i] = (
                    new Regex(rules[i].Pattern, RegexOptions.Compiled),
                    rules[i].Replacement
                );
            }
        }

        /// <summary>
        /// Returns the input string with sensitive patterns replaced by redaction placeholders.
        /// Returns null/empty inputs unchanged.
        /// </summary>
        public static string Redact(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var result = input;
            for (int i = 0; i < RedactionRules.Length; i++)
            {
                result = RedactionRules[i].Regex.Replace(result, RedactionRules[i].Replacement);
            }
            return result;
        }

        /// <summary>
        /// Returns true if the given field name is in the allowlist of safe fields
        /// whose values should NOT be redacted.
        /// </summary>
        public static bool IsSafeField(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                return false;
            return SafeFields.Contains(fieldName);
        }
    }
}
