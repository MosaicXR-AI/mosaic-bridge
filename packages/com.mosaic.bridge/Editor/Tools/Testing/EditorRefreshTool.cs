using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using UnityEditor;

namespace Mosaic.Bridge.Tools.Testing
{
    /// <summary>
    /// Forces an AssetDatabase refresh (domain reload if scripts changed)
    /// and returns compilation status. Use this after modifying C# files
    /// to ensure the domain reload is complete before checking results.
    /// </summary>
    public static class EditorRefreshTool
    {
        [MosaicTool("editor/refresh", "Forces AssetDatabase.Refresh and returns compilation status. Call after modifying scripts to trigger domain reload.", isReadOnly: false)]
        public static ToolResult<RefreshResult> Refresh()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            var result = new RefreshResult
            {
                CompilationFailed = EditorUtility.scriptCompilationFailed,
                IsCompiling = EditorApplication.isCompiling
            };

            if (result.CompilationFailed)
            {
                return ToolResult<RefreshResult>.Fail(
                    "Script compilation failed after refresh. Check Unity Console.",
                    "COMPILATION_FAILED");
            }

            return ToolResult<RefreshResult>.Ok(result);
        }
    }

    public sealed class RefreshResult
    {
        public bool CompilationFailed { get; set; }
        public bool IsCompiling { get; set; }
    }
}
