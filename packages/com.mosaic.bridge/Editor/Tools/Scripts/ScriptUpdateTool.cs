using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Scripts
{
    public static class ScriptUpdateTool
    {
        [MosaicTool("script/update",
                    "Overwrites the content of an existing script or shader asset (.cs, .shader, .hlsl, .compute, .cginc, .asmdef, .asmref, .uss, .uxml)")]
        public static ToolResult<ScriptUpdateResult> Execute(ScriptUpdateParams p)
        {
            var fullPath = Path.Combine(
                Application.dataPath.Replace("/Assets", ""),
                p.Path);

            if (!File.Exists(fullPath))
                return ToolResult<ScriptUpdateResult>.Fail(
                    $"File not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            File.WriteAllText(fullPath, p.Content, Encoding.UTF8);
            AssetDatabase.ImportAsset(p.Path);

            return ToolResult<ScriptUpdateResult>.Ok(new ScriptUpdateResult
            {
                Path      = p.Path,
                LineCount = CountLines(p.Content)
            });
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 1;
            foreach (char c in text)
                if (c == '\n') count++;
            return count;
        }
    }
}
