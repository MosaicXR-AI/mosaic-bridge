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

        /// <summary>
        /// Ensures the given asset folder path exists in both the filesystem and AssetDatabase.
        /// Creates all intermediate folders as needed (equivalent to mkdir -p but AssetDatabase-aware).
        /// </summary>
        public static void EnsureFolder(string assetFolderPath)
        {
            assetFolderPath = assetFolderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

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
