#if MOSAIC_HAS_CINEMACHINE
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    internal static class CinemachineToolHelpers
    {
        internal static string GetHierarchyPath(Transform t)
        {
            var path = t.name;
            var current = t.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        /// <summary>Resolves a NoiseSettings profile by asset path, or by name search across the
        /// whole project (covers Cinemachine's own package-bundled presets — e.g. "Handheld",
        /// "6D Shake Medium" — as well as any project-authored profile) when only a name is given.</summary>
        internal static bool TryResolveNoiseProfile(string path, string presetName, out NoiseSettings result, out string error)
        {
            result = null;
            error = null;

            if (!string.IsNullOrEmpty(path))
            {
                result = AssetDatabase.LoadAssetAtPath<NoiseSettings>(path);
                if (result == null)
                    error = $"No NoiseSettings asset found at '{path}'";
                return result != null;
            }

            if (!string.IsNullOrEmpty(presetName))
            {
                var guids = AssetDatabase.FindAssets($"t:NoiseSettings {presetName}");
                foreach (var guid in guids)
                {
                    var candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(candidatePath) == presetName)
                    {
                        result = AssetDatabase.LoadAssetAtPath<NoiseSettings>(candidatePath);
                        return result != null;
                    }
                }
                if (guids.Length > 0)
                    result = AssetDatabase.LoadAssetAtPath<NoiseSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));

                if (result == null)
                    error = $"No NoiseSettings asset named '{presetName}' found in the project or installed packages.";
                return result != null;
            }

            return true; // both omitted — not an error, just nothing to assign
        }

        internal static bool TryParseLensModeOverride(string value, out LensSettings.OverrideModes result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "none":          result = LensSettings.OverrideModes.None;          return true;
                case "orthographic":  result = LensSettings.OverrideModes.Orthographic;  return true;
                case "perspective":   result = LensSettings.OverrideModes.Perspective;   return true;
                case "physical":      result = LensSettings.OverrideModes.Physical;      return true;
                default:              result = LensSettings.OverrideModes.None;          return false;
            }
        }
    }
}
#endif
