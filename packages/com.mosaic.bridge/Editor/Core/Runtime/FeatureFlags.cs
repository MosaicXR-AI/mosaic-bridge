using System.Collections.Generic;
using UnityEditor;

namespace Mosaic.Bridge.Core.Runtime
{
    /// <summary>
    /// Story 10.5 — Static feature flag registry backed by EditorPrefs.
    /// Controls opt-in pilot features. All flags default to false except PipelineEnabled (true).
    /// </summary>
    public static class FeatureFlags
    {
        private const string Prefix = "MosaicBridge.Feature.";

        // Known flag names
        public const string TelemetryEnabled       = "TelemetryEnabled";
        public const string FeedbackEnabled         = "FeedbackEnabled";
        public const string PipelineEnabled         = "PipelineEnabled";
        public const string ScriptApprovalEnabled   = "ScriptApprovalEnabled";
        public const string AdvancedLoggingEnabled  = "AdvancedLoggingEnabled";

        private static readonly Dictionary<string, bool> Defaults = new Dictionary<string, bool>
        {
            { TelemetryEnabled,      false },
            { FeedbackEnabled,       false },
            { PipelineEnabled,       true  },
            { ScriptApprovalEnabled, false },
            { AdvancedLoggingEnabled,false },
        };

        /// <summary>Returns true if the named flag is enabled.</summary>
        public static bool IsEnabled(string flagName)
        {
            bool defaultValue = Defaults.ContainsKey(flagName) ? Defaults[flagName] : false;
            return EditorPrefs.GetBool(Prefix + flagName, defaultValue);
        }

        /// <summary>Sets a feature flag value.</summary>
        public static void SetEnabled(string flagName, bool value)
        {
            EditorPrefs.SetBool(Prefix + flagName, value);
        }

        /// <summary>Returns a dictionary of all known flags and their current values.</summary>
        public static Dictionary<string, bool> GetAll()
        {
            var result = new Dictionary<string, bool>();
            foreach (var kvp in Defaults)
            {
                result[kvp.Key] = EditorPrefs.GetBool(Prefix + kvp.Key, kvp.Value);
            }
            return result;
        }

        /// <summary>Resets all flags to their defaults.</summary>
        public static void ResetToDefaults()
        {
            foreach (var kvp in Defaults)
            {
                EditorPrefs.SetBool(Prefix + kvp.Key, kvp.Value);
            }
        }
    }
}
