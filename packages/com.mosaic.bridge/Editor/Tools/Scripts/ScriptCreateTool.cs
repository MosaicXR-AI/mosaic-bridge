using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Scripts
{
    public static class ScriptCreateTool
    {
        private static readonly string[] AllowedExtensions =
            { ".cs", ".shader", ".hlsl", ".compute", ".cginc", ".asmdef", ".asmref", ".uss", ".uxml" };

        [MosaicTool("script/create",
                    "Creates a new script or shader asset (.cs, .shader, .hlsl, .compute, .cginc, .asmdef, .asmref, .uss, .uxml); set Overwrite=true to replace an existing file")]
        public static ToolResult<ScriptCreateResult> Execute(ScriptCreateParams p)
        {
            if (!p.Path.StartsWith("Assets/"))
                return ToolResult<ScriptCreateResult>.Fail(
                    $"Path must start with 'Assets/' — got '{p.Path}'",
                    ErrorCodes.INVALID_PARAM);

            // Validate filename characters — Unity rejects paths with < > : " | ? *
            var fileName = Path.GetFileNameWithoutExtension(p.Path);
            if (string.IsNullOrEmpty(fileName))
                return ToolResult<ScriptCreateResult>.Fail(
                    "Filename cannot be empty", ErrorCodes.INVALID_PARAM);
            if (System.Text.RegularExpressions.Regex.IsMatch(fileName, @"[<>:""|?*\x00-\x1f\\]"))
                return ToolResult<ScriptCreateResult>.Fail(
                    $"Filename '{fileName}' contains invalid characters. Avoid: < > : \" | ? * and control characters",
                    ErrorCodes.INVALID_PARAM);

            var ext = Path.GetExtension(p.Path)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                return ToolResult<ScriptCreateResult>.Fail(
                    $"Unsupported extension '{ext}'. Allowed: {string.Join(", ", AllowedExtensions)}",
                    ErrorCodes.INVALID_PARAM);

            bool existed = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.Path) != null;
            if (existed && !p.Overwrite)
                return ToolResult<ScriptCreateResult>.Fail(
                    "File already exists. Set Overwrite=true to replace.",
                    ErrorCodes.CONFLICT);

            var fullPath = Path.Combine(
                Application.dataPath.Replace("/Assets", ""),
                p.Path);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, p.Content, Encoding.UTF8);
            AssetDatabase.ImportAsset(p.Path);

            return ToolResult<ScriptCreateResult>.Ok(new ScriptCreateResult
            {
                Path      = p.Path,
                Created   = true,
                Overwrote = existed
            });
        }
    }
}
