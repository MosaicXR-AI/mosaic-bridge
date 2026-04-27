#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Mosaic.Bridge.Core.Diagnostics
{
    /// <summary>
    /// Reads the package version from package.json once at Editor load time and caches it.
    /// Editor-only — not compiled into player builds.
    /// </summary>
    [UnityEditor.InitializeOnLoad]
    public static class MosaicVersion
    {
        /// <summary>The version string from com.mosaic.bridge/package.json, e.g. "0.1.0-preview.1".</summary>
        public static readonly string Current;

        static MosaicVersion()
        {
            Current = ReadVersion();
        }

        private static string ReadVersion()
        {
            try
            {
                // Resolve from the Unity project root (parent of Assets/)
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var packageJsonPath = Path.Combine(projectRoot, "Packages", "com.mosaic.bridge", "package.json");

                if (!File.Exists(packageJsonPath))
                    return "unknown";

                var json = JObject.Parse(File.ReadAllText(packageJsonPath));
                return json["version"]?.Value<string>() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
#endif
