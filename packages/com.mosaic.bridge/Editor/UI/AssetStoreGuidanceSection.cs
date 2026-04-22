using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.UI
{
    /// <summary>
    /// Project Settings section that controls whether Mosaic Bridge suggests
    /// Asset Store search links when no matching 3D asset is found in the project.
    /// </summary>
    public static class AssetStoreGuidanceSection
    {
        // Matches AssetFind3DTool.PrefKeyStoreGuidance — keep in sync if renamed
        private const string PrefKey = "MosaicBridge.AssetStoreGuidance";

        public static void Draw()
        {
            EditorGUILayout.LabelField("Asset Search Behavior", EditorStyles.boldLabel);

            bool current = EditorPrefs.GetBool(PrefKey, true);

            EditorGUILayout.HelpBox(
                "When you ask the AI to create a complex object (ship, house, character, etc.), " +
                "Mosaic Bridge follows this decision order:\n" +
                "1. Is it a primitive? (cube, sphere, cylinder…) → build directly\n" +
                "2. Search project assets for a matching prefab or 3D model\n" +
                "3. If nothing found and Asset Store guidance is ON → AI shows you a store search link\n" +
                "4. If store guidance is OFF or you decline → AI builds procedurally with ProBuilder",
                MessageType.Info);

            EditorGUILayout.Space(4);

            bool newValue = EditorGUILayout.Toggle(
                new GUIContent(
                    "Suggest Asset Store links",
                    "When enabled, the AI will provide an Asset Store search URL when no matching " +
                    "asset is found in the project, before building procedurally."),
                current);

            if (newValue != current)
                EditorPrefs.SetBool(PrefKey, newValue);

            if (!newValue)
            {
                EditorGUILayout.HelpBox(
                    "Asset Store guidance is OFF — the AI will build all complex objects procedurally with ProBuilder.",
                    MessageType.Warning);
            }
        }
    }
}
