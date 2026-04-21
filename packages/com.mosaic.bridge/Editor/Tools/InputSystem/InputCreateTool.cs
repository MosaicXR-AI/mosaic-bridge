#if MOSAIC_HAS_INPUT_SYSTEM
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.InputSystem
{
    public static class InputCreateTool
    {
        [MosaicTool("input/create",
                    "Creates a new InputActionAsset at the specified project path",
                    isReadOnly: false)]
        public static ToolResult<InputCreateResult> Execute(InputCreateParams p)
        {
            if (!p.Path.StartsWith("Assets/"))
                return ToolResult<InputCreateResult>.Fail(
                    "Path must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (!p.Path.EndsWith(".inputactions"))
                return ToolResult<InputCreateResult>.Fail(
                    "Path must end with '.inputactions'", ErrorCodes.INVALID_PARAM);

            if (AssetDatabase.AssetPathExists(p.Path))
                return ToolResult<InputCreateResult>.Fail(
                    $"Asset already exists at '{p.Path}'", ErrorCodes.CONFLICT);

            // Ensure directory exists
            var absoluteDir = Path.GetDirectoryName(
                Path.Combine(Application.dataPath, "..", p.Path));
            if (!string.IsNullOrEmpty(absoluteDir))
                Directory.CreateDirectory(absoluteDir);

            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = p.Name;

            // A freshly-created InputActionAsset has null internal collections
            // (m_ActionMaps, m_ControlSchemes). InputActionAsset.ToJson() iterates
            // those via LINQ which throws ArgumentNullException with
            // "Value cannot be null. Parameter name: source". Seed an empty
            // default ActionMap so ToJson has a concrete collection to serialize
            // — callers can rename/remove it later via input/map tools.
            asset.AddActionMap("Default");

            // InputActionAsset is serialized as JSON text
            File.WriteAllText(
                Path.Combine(Application.dataPath, "..", p.Path),
                asset.ToJson());
            Object.DestroyImmediate(asset);

            AssetDatabase.ImportAsset(p.Path);
            var imported = AssetDatabase.LoadAssetAtPath<InputActionAsset>(p.Path);
            if (imported != null)
                Undo.RegisterCreatedObjectUndo(imported, "Mosaic: Create InputActionAsset");

            var guid = AssetDatabase.AssetPathToGUID(p.Path);

            return ToolResult<InputCreateResult>.Ok(new InputCreateResult
            {
                Name      = p.Name,
                AssetPath = p.Path,
                Guid      = guid
            });
        }
    }
}
#endif
