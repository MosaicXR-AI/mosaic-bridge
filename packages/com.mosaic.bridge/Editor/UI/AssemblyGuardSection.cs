using System.Collections.Generic;
using Mosaic.Bridge.Core.Security;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Draws the "Allowed Tool Assemblies" section inside the Mosaic Bridge
    /// settings page. Allows users to manage which external assemblies can
    /// register tools with the bridge.
    /// </summary>
    public static class AssemblyGuardSection
    {
        private static string _newAssemblyName = "";

        /// <summary>
        /// Draws the full assembly guard section.
        /// Call from <see cref="MosaicBridgeSettingsProvider.OnGUI"/>.
        /// </summary>
        public static void Draw()
        {
            EditorGUILayout.LabelField("Allowed Tool Assemblies", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Only assemblies on this list can register tools with the Mosaic Bridge. " +
                "Third-party packages that reference Mosaic.Bridge.Contracts can define " +
                "[MosaicTool] methods, but those tools will be ignored unless the assembly " +
                "is explicitly allowed here.",
                MessageType.Info);

            var assemblies = AssemblyGuard.GetAllowedAssemblies();

            // ── Current entries ──────────────────────────────────────────────
            string toRemove = null;

            foreach (var asm in assemblies)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isDefault = AssemblyGuard.IsDefault(asm);

                    // Lock icon for defaults
                    if (isDefault)
                    {
                        GUILayout.Label(EditorGUIUtility.IconContent("LockIcon-On"),
                            GUILayout.Width(20), GUILayout.Height(18));
                    }
                    else
                    {
                        GUILayout.Space(24);
                    }

                    EditorGUILayout.LabelField(asm);

                    using (new EditorGUI.DisabledGroupScope(isDefault))
                    {
                        if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60)))
                        {
                            toRemove = asm;
                        }
                    }
                }
            }

            // Apply removal outside iteration
            if (toRemove != null)
            {
                AssemblyGuard.Revoke(toRemove);
            }

            // ── Add new assembly ─────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Add Assembly", EditorStyles.miniBoldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _newAssemblyName = EditorGUILayout.TextField(_newAssemblyName);

                using (new EditorGUI.DisabledGroupScope(string.IsNullOrWhiteSpace(_newAssemblyName)))
                {
                    if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        AssemblyGuard.Allow(_newAssemblyName.Trim());
                        _newAssemblyName = "";
                        GUI.FocusControl(null);
                    }
                }
            }
        }
    }
}
