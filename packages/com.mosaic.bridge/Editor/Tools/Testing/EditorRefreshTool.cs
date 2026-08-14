using System;
using System.IO;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using UnityEditor;
using UnityEngine;

namespace Mosaic.Bridge.Tools.Testing
{
    /// <summary>
    /// Forces an AssetDatabase refresh and reports whether the compiled assemblies actually moved.
    ///
    /// The previous version returned `CompilationFailed:false, IsCompiling:false` and nothing else,
    /// which reads as "your code is compiled and current". It is not the same claim. Refresh only
    /// QUEUES work, so `isCompiling` sampled immediately afterwards is almost always false because
    /// the compile has not started yet — the honest-looking answer arrives before the thing it
    /// describes.
    ///
    /// Observed cost: a caller edited sources, called this twice, was told compilation had not
    /// failed, and worked for half an hour against an assembly built before the edits. The
    /// ScriptAssemblies timestamp had not moved the whole time.
    ///
    /// So this reports the assembly timestamp, whether it changed, and whether Auto Refresh is even
    /// enabled — because a refresh that cannot take effect must not answer like one that did.
    /// </summary>
    public static class EditorRefreshTool
    {
        [MosaicTool("editor/refresh",
            "Forces AssetDatabase.Refresh and reports whether assemblies were actually rebuilt. " +
            "Refresh only QUEUES compilation, so AssembliesRebuilt=false with Pending=true means " +
            "the rebuild has not happened yet — poll again rather than assuming your code is live.",
            isReadOnly: false)]
        public static ToolResult<RefreshResult> Refresh()
        {
            var before = NewestAssemblyWriteUtc();

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            var after = NewestAssemblyWriteUtc();
            bool autoRefresh = IsAutoRefreshEnabled();
            bool compiling = EditorApplication.isCompiling;
            bool rebuilt = after > before;

            var result = new RefreshResult
            {
                CompilationFailed  = EditorUtility.scriptCompilationFailed,
                IsCompiling        = compiling,
                AssembliesRebuilt  = rebuilt,
                AssembliesUpdatedUtc = after == DateTime.MinValue ? null : after.ToString("o"),
                AutoRefreshEnabled = autoRefresh,
                Pending            = compiling || (!rebuilt && autoRefresh),
            };

            if (result.CompilationFailed)
                return ToolResult<RefreshResult>.Fail(
                    "Script compilation failed after refresh. Check Unity Console.",
                    "COMPILATION_FAILED");

            // The case that burned half an hour: nothing rebuilt, nothing is compiling, and Auto
            // Refresh is off, so nothing is going to rebuild either. Saying so is the entire value
            // of this tool over a bare Refresh() call.
            if (!rebuilt && !compiling && !autoRefresh)
            {
                result.Message =
                    "Assemblies were NOT rebuilt and Auto Refresh is disabled in Editor " +
                    "preferences, so script edits will not take effect. Enable " +
                    "Preferences > Asset Pipeline > Auto Refresh, or restart the Editor. " +
                    "Anything you call now runs against the previously compiled assemblies.";
                return result.AssembliesUpdatedUtc == null
                    ? ToolResult<RefreshResult>.Ok(result)
                    : ToolResult<RefreshResult>.Fail(result.Message, "STALE_ASSEMBLIES");
            }

            if (!rebuilt && !compiling)
                result.Message = "Nothing needed rebuilding — no script changes were detected.";
            else if (!rebuilt)
                result.Message = "Compilation is in flight; call again to confirm it completed.";
            else
                result.Message = "Assemblies rebuilt.";

            return ToolResult<RefreshResult>.Ok(result);
        }

        /// <summary>Newest write time across Library/ScriptAssemblies, or MinValue when absent.</summary>
        private static DateTime NewestAssemblyWriteUtc()
        {
            try
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(projectRoot)) return DateTime.MinValue;
                var dir = Path.Combine(projectRoot, "Library", "ScriptAssemblies");
                if (!Directory.Exists(dir)) return DateTime.MinValue;

                var newest = DateTime.MinValue;
                foreach (var f in Directory.GetFiles(dir, "*.dll"))
                {
                    var t = File.GetLastWriteTimeUtc(f);
                    if (t > newest) newest = t;
                }
                return newest;
            }
            catch (Exception)
            {
                // Reporting is best-effort; never fail a refresh because a timestamp was unreadable.
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// Auto Refresh state. Unity has moved this preference key between versions, so both the
        /// current tri-state and the older boolean are consulted; unknown means assume enabled,
        /// since a false "it is disabled" would be its own misleading answer.
        /// </summary>
        private static bool IsAutoRefreshEnabled()
        {
            try
            {
                if (EditorPrefs.HasKey("kAutoRefreshMode"))
                    return EditorPrefs.GetInt("kAutoRefreshMode", 1) != 0;
                if (EditorPrefs.HasKey("kAutoRefresh"))
                    return EditorPrefs.GetBool("kAutoRefresh", true);
                return true;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    public sealed class RefreshResult
    {
        public bool CompilationFailed { get; set; }
        public bool IsCompiling { get; set; }

        /// <summary>True when Library/ScriptAssemblies actually changed during this call.</summary>
        public bool AssembliesRebuilt { get; set; }

        /// <summary>ISO-8601 UTC write time of the newest compiled assembly, or null.</summary>
        public string AssembliesUpdatedUtc { get; set; }

        /// <summary>False means script edits will not take effect until this is enabled.</summary>
        public bool AutoRefreshEnabled { get; set; }

        /// <summary>True when a rebuild is still expected — call again before trusting the result.</summary>
        public bool Pending { get; set; }

        /// <summary>Plain-language statement of what happened, and what to do if nothing did.</summary>
        public string Message { get; set; }
    }
}
