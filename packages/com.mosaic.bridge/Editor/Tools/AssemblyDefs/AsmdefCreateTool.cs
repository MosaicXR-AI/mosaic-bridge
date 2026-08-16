using System;
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
                    "Creates a new .asmdef (Assembly Definition) in the given FOLDER, named after " +
                    "Name — e.g. Path='Assets/Scripts/Runtime', Name='Game.Runtime' writes " +
                    "Assets/Scripts/Runtime/Game.Runtime.asmdef. Passing the .asmdef file path " +
                    "itself also works; the folder is taken from it.",
                    isReadOnly: false)]
        public static ToolResult<AsmdefCreateResult> Create(AsmdefCreateParams p)
        {
            if (!p.Path.StartsWith("Assets/") && !p.Path.StartsWith("Assets\\"))
                return ToolResult<AsmdefCreateResult>.Fail(
                    "Path must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            // Path is a FOLDER, but the tool description used to say "at the specified path", which
            // reads as a file path — so callers passed `Assets/Scripts/Runtime.asmdef`. EnsureFolder
            // then created a directory literally named `Runtime.asmdef` and wrote
            // `Runtime.asmdef/Runtime.asmdef` inside it. The call SUCCEEDED and returned a FilePath,
            // leaving a bogus folder in the project and an assembly Unity would never pick up where
            // it was wanted.
            //
            // A folder whose name ends in .asmdef is never what anyone meant, so accept the file
            // path as an alias for its folder rather than making the caller guess which of the two
            // descriptions was true.
            var folder = p.Path;
            if (folder.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
            {
                folder = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(folder))
                    return ToolResult<AsmdefCreateResult>.Fail(
                        $"'{p.Path}' names an .asmdef file with no folder above it. Pass the folder " +
                        "it belongs in, e.g. 'Assets/Scripts/Runtime'.", ErrorCodes.INVALID_PARAM);
            }

            AssetDatabaseHelper.EnsureFolder(folder);
            var fullDir = Path.GetFullPath(folder);

            var filePath = Path.Combine(folder, p.Name + ".asmdef");
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
