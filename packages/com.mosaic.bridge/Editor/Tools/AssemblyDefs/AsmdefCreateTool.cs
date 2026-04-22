using System.IO;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Tools.AssemblyDefs
{
    public static class AsmdefCreateTool
    {
        [MosaicTool("asmdef/create",
                    "Creates a new .asmdef (Assembly Definition) file at the specified path",
                    isReadOnly: false)]
        public static ToolResult<AsmdefCreateResult> Create(AsmdefCreateParams p)
        {
            if (!p.Path.StartsWith("Assets/") && !p.Path.StartsWith("Assets\\"))
                return ToolResult<AsmdefCreateResult>.Fail(
                    "Path must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            AssetDatabaseHelper.EnsureFolder(p.Path);
            var fullDir = Path.GetFullPath(p.Path);

            var filePath = Path.Combine(p.Path, p.Name + ".asmdef");
            var fullFilePath = Path.GetFullPath(filePath);

            if (File.Exists(fullFilePath))
                return ToolResult<AsmdefCreateResult>.Fail(
                    $"Assembly definition already exists at '{filePath}'", ErrorCodes.CONFLICT);

            var asmdef = new JObject
            {
                ["name"] = p.Name,
                ["rootNamespace"] = p.RootNamespace ?? "",
                ["references"] = new JArray(p.References ?? new string[0]),
                ["includePlatforms"] = new JArray(p.IncludePlatforms ?? new string[0]),
                ["excludePlatforms"] = new JArray(),
                ["allowUnsafeCode"] = false,
                ["overrideReferences"] = false,
                ["precompiledReferences"] = new JArray(),
                ["autoReferenced"] = true,
                ["defineConstraints"] = new JArray(),
                ["versionDefines"] = new JArray(),
                ["noEngineReferences"] = false
            };

            File.WriteAllText(fullFilePath, asmdef.ToString(Formatting.Indented));
            AssetDatabase.ImportAsset(filePath);

            return ToolResult<AsmdefCreateResult>.Ok(new AsmdefCreateResult
            {
                Name           = p.Name,
                FilePath       = filePath,
                ReferenceCount = p.References?.Length ?? 0
            });
        }
    }
}
