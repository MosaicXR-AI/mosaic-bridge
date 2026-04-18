using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Scripts
{
    public static class ScriptReadTool
    {
        [MosaicTool("script/read",
                    "Reads the source content of a script or shader asset (.cs, .shader, .hlsl, .compute, .cginc, .asmdef, .asmref, .uss, .uxml)",
                    isReadOnly: true)]
        public static ToolResult<ScriptReadResult> Execute(ScriptReadParams p)
        {
            // Try MonoScript first (for .cs files)
            var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(p.Path);
            if (monoScript != null)
            {
                var content   = monoScript.text;
                int lineCount = CountLines(content);
                return ToolResult<ScriptReadResult>.Ok(new ScriptReadResult
                {
                    Path      = p.Path,
                    Content   = content,
                    LineCount = lineCount
                });
            }

            // For non-.cs files (.shader, .hlsl, .compute, etc.), read from disk
            var fullPath = Path.Combine(
                Application.dataPath.Replace("/Assets", ""),
                p.Path);

            if (!File.Exists(fullPath))
                return ToolResult<ScriptReadResult>.Fail(
                    $"File not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            var fileContent = File.ReadAllText(fullPath);
            return ToolResult<ScriptReadResult>.Ok(new ScriptReadResult
            {
                Path      = p.Path,
                Content   = fileContent,
                LineCount = CountLines(fileContent)
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
