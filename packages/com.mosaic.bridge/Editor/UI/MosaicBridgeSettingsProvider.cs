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
                keywords = new[] { "Mosaic", "Bridge", "MCP", "Pipeline", "Assembly", "Tools", "Feature", "Flags", "Telemetry", "Particle", "Pack", "VFX", "Effects" }
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
        }
    }
}
