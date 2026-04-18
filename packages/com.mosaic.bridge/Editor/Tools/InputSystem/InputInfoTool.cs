#if MOSAIC_HAS_INPUT_SYSTEM
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.InputSystem;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.InputSystem
{
    public static class InputInfoTool
    {
        [MosaicTool("input/info",
                    "Queries an InputActionAsset and returns its maps, actions, and bindings",
                    isReadOnly: true)]
        public static ToolResult<InputInfoResult> Execute(InputInfoParams p)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(p.AssetPath);
            if (asset == null)
                return ToolResult<InputInfoResult>.Fail(
                    $"InputActionAsset not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var guid = AssetDatabase.AssetPathToGUID(p.AssetPath);
            var maps = new List<InputMapInfo>();

            foreach (var map in asset.actionMaps)
            {
                var actions = new List<InputActionInfo>();
                foreach (var action in map.actions)
                {
                    var bindings = action.bindings
                        .Select(b => new InputBindingInfo
                        {
                            Name              = b.name,
                            Path              = b.path,
                            IsComposite       = b.isComposite,
                            IsPartOfComposite = b.isPartOfComposite
                        })
                        .ToList();

                    actions.Add(new InputActionInfo
                    {
                        Name     = action.name,
                        Type     = action.type.ToString(),
                        Bindings = bindings
                    });
                }

                maps.Add(new InputMapInfo
                {
                    Name    = map.name,
                    Actions = actions
                });
            }

            return ToolResult<InputInfoResult>.Ok(new InputInfoResult
            {
                AssetPath = p.AssetPath,
                Name      = asset.name,
                Guid      = guid,
                Maps      = maps
            });
        }
    }
}
#endif
