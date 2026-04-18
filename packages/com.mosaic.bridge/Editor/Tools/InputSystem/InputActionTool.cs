#if MOSAIC_HAS_INPUT_SYSTEM
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.InputSystem
{
    public static class InputActionTool
    {
        [MosaicTool("input/action",
                    "Manages actions within an action map (add, remove, add-binding, add-composite)",
                    isReadOnly: false)]
        public static ToolResult<InputActionResult> Execute(InputActionParams p)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(p.AssetPath);
            if (asset == null)
                return ToolResult<InputActionResult>.Fail(
                    $"InputActionAsset not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var map = asset.FindActionMap(p.MapName);
            if (map == null)
                return ToolResult<InputActionResult>.Fail(
                    $"Action map '{p.MapName}' not found in asset", ErrorCodes.NOT_FOUND);

            var action = p.Action?.ToLowerInvariant();
            switch (action)
            {
                case "add":
                    return AddAction(asset, map, p);

                case "remove":
                    return RemoveAction(asset, map, p);

                case "add-binding":
                    return AddBinding(asset, map, p);

                case "add-composite":
                    return AddComposite(asset, map, p);

                default:
                    return ToolResult<InputActionResult>.Fail(
                        $"Invalid action '{p.Action}'. Must be 'add', 'remove', 'add-binding', or 'add-composite'",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<InputActionResult> AddAction(
            InputActionAsset asset, InputActionMap map, InputActionParams p)
        {
            if (string.IsNullOrEmpty(p.ActionName))
                return ToolResult<InputActionResult>.Fail(
                    "ActionName is required for 'add' action", ErrorCodes.INVALID_PARAM);

            if (map.FindAction(p.ActionName) != null)
                return ToolResult<InputActionResult>.Fail(
                    $"Action '{p.ActionName}' already exists in map '{p.MapName}'", ErrorCodes.CONFLICT);

            Undo.RecordObject(asset, "Mosaic: Add Input Action");

            var actionType = InputActionType.Button;
            if (!string.IsNullOrEmpty(p.ActionType))
            {
                if (!Enum.TryParse<InputActionType>(p.ActionType, true, out actionType))
                    return ToolResult<InputActionResult>.Fail(
                        $"Invalid ActionType '{p.ActionType}'. Must be 'Button', 'Value', or 'PassThrough'",
                        ErrorCodes.INVALID_PARAM);
            }

            map.AddAction(p.ActionName, actionType);
            SaveAsset(asset, p.AssetPath);

            return ToolResult<InputActionResult>.Ok(BuildResult(asset, map, p, "add"));
        }

        private static ToolResult<InputActionResult> RemoveAction(
            InputActionAsset asset, InputActionMap map, InputActionParams p)
        {
            if (string.IsNullOrEmpty(p.ActionName))
                return ToolResult<InputActionResult>.Fail(
                    "ActionName is required for 'remove' action", ErrorCodes.INVALID_PARAM);

            var inputAction = map.FindAction(p.ActionName);
            if (inputAction == null)
                return ToolResult<InputActionResult>.Fail(
                    $"Action '{p.ActionName}' not found in map '{p.MapName}'", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(asset, "Mosaic: Remove Input Action");
            inputAction.RemoveAction();
            SaveAsset(asset, p.AssetPath);

            return ToolResult<InputActionResult>.Ok(BuildResult(asset, map, p, "remove"));
        }

        private static ToolResult<InputActionResult> AddBinding(
            InputActionAsset asset, InputActionMap map, InputActionParams p)
        {
            if (string.IsNullOrEmpty(p.ActionName))
                return ToolResult<InputActionResult>.Fail(
                    "ActionName is required for 'add-binding' action", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.BindingPath))
                return ToolResult<InputActionResult>.Fail(
                    "BindingPath is required for 'add-binding' action", ErrorCodes.INVALID_PARAM);

            var inputAction = map.FindAction(p.ActionName);
            if (inputAction == null)
                return ToolResult<InputActionResult>.Fail(
                    $"Action '{p.ActionName}' not found in map '{p.MapName}'", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(asset, "Mosaic: Add Input Binding");
            inputAction.AddBinding(p.BindingPath);
            SaveAsset(asset, p.AssetPath);

            return ToolResult<InputActionResult>.Ok(BuildResult(asset, map, p, "add-binding"));
        }

        private static ToolResult<InputActionResult> AddComposite(
            InputActionAsset asset, InputActionMap map, InputActionParams p)
        {
            if (string.IsNullOrEmpty(p.ActionName))
                return ToolResult<InputActionResult>.Fail(
                    "ActionName is required for 'add-composite' action", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.CompositeType))
                return ToolResult<InputActionResult>.Fail(
                    "CompositeType is required for 'add-composite' action", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.CompositePart))
                return ToolResult<InputActionResult>.Fail(
                    "CompositePart is required for 'add-composite' action", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.BindingPath))
                return ToolResult<InputActionResult>.Fail(
                    "BindingPath is required for 'add-composite' action", ErrorCodes.INVALID_PARAM);

            var inputAction = map.FindAction(p.ActionName);
            if (inputAction == null)
                return ToolResult<InputActionResult>.Fail(
                    $"Action '{p.ActionName}' not found in map '{p.MapName}'", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(asset, "Mosaic: Add Input Composite");
            inputAction.AddCompositeBinding(p.CompositeType)
                .With(p.CompositePart, p.BindingPath);
            SaveAsset(asset, p.AssetPath);

            return ToolResult<InputActionResult>.Ok(BuildResult(asset, map, p, "add-composite"));
        }

        private static InputActionResult BuildResult(
            InputActionAsset asset, InputActionMap map, InputActionParams p, string action)
        {
            // Reload the map from the asset after save
            var updatedMap = asset.FindActionMap(p.MapName);
            var actions = updatedMap?.actions.Select(a => a.name).ToList() ?? new List<string>();
            var bindings = new List<string>();

            if (!string.IsNullOrEmpty(p.ActionName) && updatedMap != null)
            {
                var act = updatedMap.FindAction(p.ActionName);
                if (act != null)
                    bindings = act.bindings.Select(b => b.path ?? b.name ?? "").ToList();
            }

            return new InputActionResult
            {
                AssetPath  = p.AssetPath,
                Action     = action,
                MapName    = p.MapName,
                ActionName = p.ActionName,
                Actions    = actions,
                Bindings   = bindings
            };
        }

        private static void SaveAsset(InputActionAsset asset, string assetPath)
        {
            var json = asset.ToJson();
            File.WriteAllText(
                Path.Combine(Application.dataPath, "..", assetPath), json);
            EditorUtility.SetDirty(asset);
            AssetDatabase.ImportAsset(assetPath);
        }
    }
}
#endif
