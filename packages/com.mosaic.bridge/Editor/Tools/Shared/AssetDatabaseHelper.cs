using System;
using System.IO;
using UnityEditor;

namespace Mosaic.Bridge.Tools.Shared
{
    public static class AssetDatabaseHelper
    {
        /// <summary>
        /// Imports a .unitypackage.
        ///
        /// Unity 6.6 deprecated <c>AssetDatabase.ImportPackage</c> in favour of
        /// <c>UnityEditor.AssetPackage.Package.Import</c>, but that type does not exist in
        /// 6000.6.0a2 — it landed partway through the 6.6 cycle, and Unity emits no define
        /// granular enough to tell a2 from b5. Since the deprecation is only a warning
        /// (the call still works), suppress it here rather than guess a version boundary.
        /// Switch to the new API once it is available across all supported 6.6 builds, or
        /// when Unity promotes this to an error.
        /// </summary>
        public static void ImportPackage(string packagePath, bool interactive)
        {
#pragma warning disable 618
            AssetDatabase.ImportPackage(packagePath, interactive);
#pragma warning restore 618
        }

        /// <summary>Extensions that are never a folder name — a path ending in one is a file.</summary>
        /// <remarks>
        /// Deliberately a list of asset extensions rather than "contains a dot": `Assets/Game.Runtime`
        /// is an ordinary and common folder name, especially beside an assembly definition, so
        /// rejecting every dot would break more than it fixes.
        /// </remarks>
        private static readonly string[] NotFolderExtensions =
        {
            ".asmdef", ".asmref", ".cs", ".unity", ".prefab", ".asset", ".mat", ".shader",
            ".uxml", ".uss", ".png", ".jpg", ".json", ".txt", ".anim", ".controller"
        };

        /// <summary>
        /// Ensures the given asset folder path exists in both the filesystem and AssetDatabase.
        /// Creates all intermediate folders as needed (equivalent to mkdir -p but AssetDatabase-aware).
        ///
        /// Accepts either a folder ("Assets/Scripts/Runtime") or a file path within it
        /// ("Assets/Scripts/Runtime/Game.asmdef") — the latter ensures the folder above.
        /// </summary>
        public static void EnsureFolder(string assetFolderPath)
        {
            assetFolderPath = assetFolderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            // Accept a FILE path as well as a folder, and ensure the folder above it.
            //
            // This used to create whatever it was handed, so a caller passing a file path got a
            // DIRECTORY of that name: `asmdef/create` given `Assets/Scripts/Runtime.asmdef` built
            // `Runtime.asmdef/Runtime.asmdef` and reported success, leaving debris with no error to
            // trace it by. Rejecting file paths would fix that, but there is nothing to decide
            // here — nobody has ever wanted a folder called `Runtime.asmdef`, and the folder above
            // it is unambiguously what they meant. Twelve tools call this, so handling both fixes
            // the class rather than the instance.
            foreach (var ext in NotFolderExtensions)
            {
                if (!assetFolderPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) continue;
                var parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(parent))
                    throw new ArgumentException(
                        $"'{assetFolderPath}' names a file with no folder above it.",
                        nameof(assetFolderPath));
                EnsureFolder(parent);
                return;
            }

            var parts = assetFolderPath.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>
        /// Ensures the folder containing the given asset path exists.
        /// For "Assets/Generated/Meshes/foo.asset", ensures "Assets/Generated/Meshes" exists.
        /// </summary>
        public static void EnsureFolderForAsset(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir))
                EnsureFolder(dir);
        }
    }
}
