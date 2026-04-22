using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Unity Project Settings page for Mosaic Bridge.
    /// Registered at "Project/Mosaic Bridge" via the <see cref="SettingsProvider"/> attribute.
    /// Exposes bridge status, pipeline configuration, and license management in one place.
    /// </summary>
    public class MosaicBridgeSettingsProvider : SettingsProvider
    {
        private const string SettingsPath = "Project/Mosaic Bridge";

        public MosaicBridgeSettingsProvider()
            : base(SettingsPath, SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new MosaicBridgeSettingsProvider
            {
                keywords = new[] { "Mosaic", "Bridge", "MCP", "Pipeline", "License", "Trial", "Assembly", "Tools", "Feature", "Flags", "Telemetry", "Particle", "Pack", "VFX", "Effects" }
            };
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space(10);

            // === Bridge Status ===
            EditorGUILayout.LabelField("Bridge Status", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledGroupScope(true))
            {
                var state = Core.Bootstrap.BridgeBootstrap.State.ToString();
                var port = Core.Bootstrap.BridgeBootstrap.Server?.Port ?? 0;
                var toolCount = Core.Bootstrap.BridgeBootstrap.ToolRegistry?.Count ?? 0;
                EditorGUILayout.TextField("State", state);
                EditorGUILayout.IntField("Port", port);
                EditorGUILayout.IntField("Tools Registered", toolCount);
            }

            EditorGUILayout.Space(10);

            // === Pipeline Settings (drawn by PipelineSettingsSection) ===
            PipelineSettingsSection.Draw();

            EditorGUILayout.Space(10);

            // === Allowed Tool Assemblies (Assembly Guard) ===
            AssemblyGuardSection.Draw();

            EditorGUILayout.Space(10);

            // === Asset Store Guidance ===
            AssetStoreGuidanceSection.Draw();

            EditorGUILayout.Space(10);

            // === Particle Pack Source ===
            ParticlePackSection.Draw();

            EditorGUILayout.Space(10);

            // === Feature Flags (Story 10.5) ===
            FeatureFlagsSection.Draw();

            EditorGUILayout.Space(10);

            // === License Status ===
            EditorGUILayout.LabelField("License", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledGroupScope(true))
            {
                var tier = EditorPrefs.GetString("MosaicBridge.LicenseTier", "trial");
                EditorGUILayout.TextField("Current Tier", tier);
            }

            EditorGUILayout.Space(5);

            // License key activation
            EditorGUILayout.LabelField("Activate License", EditorStyles.miniBoldLabel);
            _licenseKeyInput = EditorGUILayout.TextField("License Key", _licenseKeyInput ?? "");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Activate", GUILayout.Width(100)))
                {
                    var activator = new Core.Licensing.LicenseActivator();
                    var result = activator.Activate(_licenseKeyInput);
                    if (result.IsSuccess)
                        EditorUtility.DisplayDialog("License Activated",
                            $"Successfully activated {result.Tier} license.", "OK");
                    else
                        EditorUtility.DisplayDialog("Activation Failed",
                            result.ErrorMessage, "OK");
                }

                if (GUILayout.Button("Deactivate", GUILayout.Width(100)))
                {
                    var activator = new Core.Licensing.LicenseActivator();
                    activator.Deactivate();
                    EditorUtility.DisplayDialog("License Deactivated",
                        "Reverted to trial mode.", "OK");
                }
            }

        }

        private string _licenseKeyInput;
    }
}
