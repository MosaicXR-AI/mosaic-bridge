using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Tools.AssemblyDefs
{
    public static class AsmdefManageTool
    {
        [MosaicTool("asmdef/manage",
                    "Manages existing .asmdef files: info, list all, add references, or set platforms",
                    isReadOnly: false)]
        public static ToolResult<AsmdefManageResult> Manage(AsmdefManageParams p)
        {
            var action = p.Action?.Trim().ToLowerInvariant();

            switch (action)
            {
                case "list":
                    return ListAll();
                case "info":
                    return Info(p.Path);
                case "add-references":
                    return AddReferences(p.Path, p.References);
                case "set-platforms":
                    return SetPlatforms(p.Path, p.Platforms);
                default:
                    return ToolResult<AsmdefManageResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: info, list, add-references, set-platforms",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AsmdefManageResult> ListAll()
        {
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();

            return ToolResult<AsmdefManageResult>.Ok(new AsmdefManageResult
            {
                Action   = "list",
                AllPaths = paths
            });
        }

        private static ToolResult<AsmdefManageResult> Info(string path)
        {
            if (string.IsNullOrEmpty(path))
                return ToolResult<AsmdefManageResult>.Fail(
                    "Path is required for info action", ErrorCodes.INVALID_PARAM);

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                return ToolResult<AsmdefManageResult>.Fail(
                    $"Assembly definition not found at '{path}'", ErrorCodes.NOT_FOUND);

            var json = JObject.Parse(File.ReadAllText(fullPath));

            return ToolResult<AsmdefManageResult>.Ok(new AsmdefManageResult
            {
                Action           = "info",
                Name             = json["name"]?.Value<string>(),
                FilePath         = path,
                References       = json["references"]?.ToObject<string[]>() ?? new string[0],
                IncludePlatforms = json["includePlatforms"]?.ToObject<string[]>() ?? new string[0],
                RootNamespace    = json["rootNamespace"]?.Value<string>() ?? ""
            });
        }

        private static ToolResult<AsmdefManageResult> AddReferences(string path, string[] references)
        {
            if (string.IsNullOrEmpty(path))
                return ToolResult<AsmdefManageResult>.Fail(
                    "Path is required for add-references action", ErrorCodes.INVALID_PARAM);
            if (references == null || references.Length == 0)
                return ToolResult<AsmdefManageResult>.Fail(
                    "References array is required for add-references action", ErrorCodes.INVALID_PARAM);

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                return ToolResult<AsmdefManageResult>.Fail(
                    $"Assembly definition not found at '{path}'", ErrorCodes.NOT_FOUND);

            var json = JObject.Parse(File.ReadAllText(fullPath));
            var existing = json["references"]?.ToObject<List<string>>() ?? new List<string>();
            foreach (var r in references)
            {
                if (!existing.Contains(r))
                    existing.Add(r);
            }
            json["references"] = new JArray(existing.ToArray());
            File.WriteAllText(fullPath, json.ToString(Formatting.Indented));
            AssetDatabase.ImportAsset(path);

            return ToolResult<AsmdefManageResult>.Ok(new AsmdefManageResult
            {
                Action     = "add-references",
                Name       = json["name"]?.Value<string>(),
                FilePath   = path,
                References = existing.ToArray()
            });
        }

        private static ToolResult<AsmdefManageResult> SetPlatforms(string path, string[] platforms)
        {
            if (string.IsNullOrEmpty(path))
                return ToolResult<AsmdefManageResult>.Fail(
                    "Path is required for set-platforms action", ErrorCodes.INVALID_PARAM);

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                return ToolResult<AsmdefManageResult>.Fail(
                    $"Assembly definition not found at '{path}'", ErrorCodes.NOT_FOUND);

            var json = JObject.Parse(File.ReadAllText(fullPath));
            json["includePlatforms"] = new JArray(platforms ?? new string[0]);
            File.WriteAllText(fullPath, json.ToString(Formatting.Indented));
            AssetDatabase.ImportAsset(path);

            return ToolResult<AsmdefManageResult>.Ok(new AsmdefManageResult
            {
                Action           = "set-platforms",
                Name             = json["name"]?.Value<string>(),
                FilePath         = path,
                IncludePlatforms = platforms ?? new string[0]
            });
        }
    }
}
