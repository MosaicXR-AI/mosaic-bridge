using System.IO;
using UnityEditor;

namespace Mosaic.Bridge.Tools.Shared
{
    public static class AssetDatabaseHelper
    {
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
