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
    public static class InputMapTool
    {
        [MosaicTool("input/map",
                    "Manages action maps on an InputActionAsset (add, remove, or list)",
                    isReadOnly: false)]
        public static ToolResult<InputMapResult> Execute(InputMapParams p)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(p.AssetPath);
            if (asset == null)
                return ToolResult<InputMapResult>.Fail(
                    $"InputActionAsset not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var action = p.Action?.ToLowerInvariant();
            switch (action)
            {
                case "list":
                    return ListMaps(asset, p.AssetPath);

                case "add":
                    if (string.IsNullOrEmpty(p.MapName))
                        return ToolResult<InputMapResult>.Fail(
                            "MapName is required for 'add' action", ErrorCodes.INVALID_PARAM);
                    return AddMap(asset, p.AssetPath, p.MapName);

                case "remove":
                    if (string.IsNullOrEmpty(p.MapName))
                        return ToolResult<InputMapResult>.Fail(
                            "MapName is required for 'remove' action", ErrorCodes.INVALID_PARAM);
                    return RemoveMap(asset, p.AssetPath, p.MapName);

                default:
                    return ToolResult<InputMapResult>.Fail(
                        $"Invalid action '{p.Action}'. Must be 'add', 'remove', or 'list'",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<InputMapResult> ListMaps(InputActionAsset asset, string assetPath)
        {
            var maps = asset.actionMaps.Select(m => m.name).ToList();
            return ToolResult<InputMapResult>.Ok(new InputMapResult
            {
                AssetPath = assetPath,
                Action    = "list",
                Maps      = maps
            });
        }

        private static ToolResult<InputMapResult> AddMap(InputActionAsset asset, string assetPath, string mapName)
        {
            if (asset.FindActionMap(mapName) != null)
                return ToolResult<InputMapResult>.Fail(
                    $"Action map '{mapName}' already exists", ErrorCodes.CONFLICT);

            Undo.RecordObject(asset, "Mosaic: Add Action Map");
            asset.AddActionMap(mapName);
            SaveAsset(asset, assetPath);

            var maps = asset.actionMaps.Select(m => m.name).ToList();
            return ToolResult<InputMapResult>.Ok(new InputMapResult
            {
                AssetPath = assetPath,
                Action    = "add",
                MapName   = mapName,
                Maps      = maps
            });
        }

        private static ToolResult<InputMapResult> RemoveMap(InputActionAsset asset, string assetPath, string mapName)
        {
            var map = asset.FindActionMap(mapName);
            if (map == null)
                return ToolResult<InputMapResult>.Fail(
                    $"Action map '{mapName}' not found", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(asset, "Mosaic: Remove Action Map");
            asset.RemoveActionMap(map);
            SaveAsset(asset, assetPath);

            var maps = asset.actionMaps.Select(m => m.name).ToList();
            return ToolResult<InputMapResult>.Ok(new InputMapResult
            {
                AssetPath = assetPath,
                Action    = "remove",
                MapName   = mapName,
                Maps      = maps
            });
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
