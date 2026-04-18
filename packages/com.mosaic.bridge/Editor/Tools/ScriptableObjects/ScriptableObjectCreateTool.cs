using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ScriptableObjects
{
    public static class ScriptableObjectCreateTool
    {
        [MosaicTool("scriptableobject/create",
                    "Creates a new ScriptableObject asset at the specified project path",
                    isReadOnly: false)]
        public static ToolResult<ScriptableObjectCreateResult> Execute(ScriptableObjectCreateParams p)
        {
            // Resolve the type by name
            var type = ResolveScriptableObjectType(p.TypeName);
            if (type == null)
                return ToolResult<ScriptableObjectCreateResult>.Fail(
                    $"Type '{p.TypeName}' not found or does not derive from ScriptableObject",
                    ErrorCodes.INVALID_PARAM);

            if (!typeof(ScriptableObject).IsAssignableFrom(type))
                return ToolResult<ScriptableObjectCreateResult>.Fail(
                    $"Type '{p.TypeName}' does not derive from ScriptableObject",
                    ErrorCodes.INVALID_PARAM);

            if (!p.AssetPath.StartsWith("Assets/"))
                return ToolResult<ScriptableObjectCreateResult>.Fail(
                    "AssetPath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (!p.AssetPath.EndsWith(".asset"))
                return ToolResult<ScriptableObjectCreateResult>.Fail(
                    "AssetPath must end with '.asset'", ErrorCodes.INVALID_PARAM);

            // Ensure directory exists
            var absoluteDir = Path.GetDirectoryName(
                Path.Combine(Application.dataPath, "..", p.AssetPath));
            if (!string.IsNullOrEmpty(absoluteDir))
                Directory.CreateDirectory(absoluteDir);

            var instance = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(instance, p.AssetPath);
            AssetDatabase.SaveAssets();

            Undo.RegisterCreatedObjectUndo(instance, "Mosaic: Create ScriptableObject");

            var guid = AssetDatabase.AssetPathToGUID(p.AssetPath);

            return ToolResult<ScriptableObjectCreateResult>.Ok(new ScriptableObjectCreateResult
            {
                AssetPath = p.AssetPath,
                TypeName  = type.FullName,
                Guid      = guid
            });
        }

        private static Type ResolveScriptableObjectType(string typeName)
        {
            // Try direct resolution first
            var t = Type.GetType(typeName);
            if (t != null && typeof(ScriptableObject).IsAssignableFrom(t))
                return t;

            // Search all loaded assemblies
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(x =>
                    (x.Name == typeName || x.FullName == typeName) &&
                    typeof(ScriptableObject).IsAssignableFrom(x));
        }
    }
}
