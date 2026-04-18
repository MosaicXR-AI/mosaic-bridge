using Mosaic.Bridge.Core.Runtime;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Story 10.5 — Draws feature flag toggles in the Mosaic Bridge Project Settings page.
    /// </summary>
    public static class FeatureFlagsSection
    {
        /// <summary>
        /// Draws the full feature flags section.
        /// Call from <see cref="MosaicBridgeSettingsProvider.OnGUI"/>.
        /// </summary>
        public static void Draw()
        {
            EditorGUILayout.LabelField("Feature Flags", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Enable or disable pilot features. Changes take effect immediately. " +
                "Telemetry data is stored locally and never sent to a server.",
                MessageType.Info);

            var flags = FeatureFlags.GetAll();

            foreach (var kvp in flags)
            {
                var label = FormatLabel(kvp.Key);
                var newValue = EditorGUILayout.Toggle(label, kvp.Value);
                if (newValue != kvp.Value)
                {
                    FeatureFlags.SetEnabled(kvp.Key, newValue);
                }
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Reset Feature Flags to Defaults"))
            {
                FeatureFlags.ResetToDefaults();
            }
        }

        /// <summary>Converts "TelemetryEnabled" to "Telemetry Enabled".</summary>
        private static string FormatLabel(string flagName)
        {
            if (string.IsNullOrEmpty(flagName))
                return flagName;

            var sb = new System.Text.StringBuilder(flagName.Length + 4);
            sb.Append(flagName[0]);

            for (int i = 1; i < flagName.Length; i++)
            {
                if (char.IsUpper(flagName[i]) && !char.IsUpper(flagName[i - 1]))
                    sb.Append(' ');
                sb.Append(flagName[i]);
            }

            return sb.ToString();
        }
    }
}
