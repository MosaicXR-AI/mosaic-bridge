using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationControllerTool
    {
        private const string ValidActions = "create, info, add-parameter, remove-parameter, add-layer, set-layer, remove-layer, create-override";

        [MosaicTool("animation/controller",
                    "Manages AnimatorController assets: create, inspect, add/remove parameters, add/set/remove " +
                    "layers, create an override controller. set-layer: LayerWeight, BlendingMode " +
                    "(Override/Additive), AvatarMaskPath (for an override layer masking body parts — " +
                    "'upper-body aim while running'), IKPass, SyncedLayerIndex/SyncedLayerAffectsTiming. " +
                    "create-override: BaseControllerPath + Overrides[] ({OriginalClipName or OriginalClipPath, " +
                    "NewClipPath}) — 'one controller, many characters'.",
                    isReadOnly: false)]
        public static ToolResult<AnimationControllerResult> Execute(AnimationControllerParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "create":          return Create(p);
                case "info":            return Info(p);
                case "add-parameter":   return AddParameter(p);
                case "remove-parameter":return RemoveParameter(p);
                case "add-layer":       return AddLayer(p);
                case "set-layer":       return SetLayer(p);
                case "remove-layer":    return RemoveLayer(p);
                case "create-override": return CreateOverride(p);
                default:
                    return ToolResult<AnimationControllerResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: {ValidActions}",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<AnimationControllerResult> Create(AnimationControllerParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationControllerResult>.Fail(
                    "Path is required for 'create' action", ErrorCodes.INVALID_PARAM);

            AnimationToolHelpers.EnsureDirectoryExists(p.Path);

            var controller = UnityEditor.Animations.AnimatorController
                .CreateAnimatorControllerAtPath(p.Path);

            if (controller == null)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"Failed to create AnimatorController at '{p.Path}'",
                    ErrorCodes.INTERNAL_ERROR);

            var guid = AssetDatabase.AssetPathToGUID(p.Path);

            return ToolResult<AnimationControllerResult>.Ok(new AnimationControllerResult
            {
                Action = "create",
                Path   = p.Path,
                Guid   = guid
            });
        }

        private static ToolResult<AnimationControllerResult> Info(AnimationControllerParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationControllerResult>.Fail(
                    "Path is required for 'info' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.Path);
            if (controller == null)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"AnimatorController not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            // Layers
            var layers = controller.layers.Select(l => l.name).ToArray();

            // Parameters
            var parameters = controller.parameters.Select(param => new AnimationParameterInfo
            {
                Name         = param.name,
                Type         = param.type.ToString(),
                DefaultFloat = param.defaultFloat,
                DefaultInt   = param.defaultInt,
                DefaultBool  = param.defaultBool
            }).ToArray();

            // States (across all layers)
            var states = new List<AnimationStateInfo>();
            foreach (var layer in controller.layers)
            {
                var defaultState = layer.stateMachine.defaultState;
                foreach (var cs in layer.stateMachine.states)
                {
                    states.Add(new AnimationStateInfo
                    {
                        Name       = cs.state.name,
                        MotionName = cs.state.motion != null ? cs.state.motion.name : null,
                        LayerName  = layer.name,
                        IsDefault  = cs.state == defaultState
                    });
                }
            }

            var guid = AssetDatabase.AssetPathToGUID(p.Path);

            return ToolResult<AnimationControllerResult>.Ok(new AnimationControllerResult
            {
                Action     = "info",
                Path       = p.Path,
                Guid       = guid,
                Layers     = layers,
                Parameters = parameters.ToArray(),
                States     = states.ToArray()
            });
        }

        private static ToolResult<AnimationControllerResult> AddParameter(AnimationControllerParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationControllerResult>.Fail(
                    "Path is required for 'add-parameter' action", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.ParameterName))
                return ToolResult<AnimationControllerResult>.Fail(
                    "ParameterName is required for 'add-parameter' action", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.ParameterType))
                return ToolResult<AnimationControllerResult>.Fail(
                    "ParameterType is required for 'add-parameter' action. Valid types: Float, Int, Bool, Trigger",
                    ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.Path);
            if (controller == null)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"AnimatorController not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            UnityEngine.AnimatorControllerParameterType paramType;
            switch (p.ParameterType.ToLowerInvariant())
            {
                case "float":   paramType = UnityEngine.AnimatorControllerParameterType.Float;   break;
                case "int":     paramType = UnityEngine.AnimatorControllerParameterType.Int;     break;
                case "bool":    paramType = UnityEngine.AnimatorControllerParameterType.Bool;    break;
                case "trigger": paramType = UnityEngine.AnimatorControllerParameterType.Trigger; break;
                default:
                    return ToolResult<AnimationControllerResult>.Fail(
                        $"Unknown parameter type '{p.ParameterType}'. Valid types: Float, Int, Bool, Trigger",
                        ErrorCodes.INVALID_PARAM);
            }

            Undo.RecordObject(controller, "Mosaic: Add Animator Parameter");
            controller.AddParameter(p.ParameterName, paramType);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationControllerResult>.Ok(new AnimationControllerResult
            {
                Action            = "add-parameter",
                Path              = p.Path,
                AddedParameterName = p.ParameterName,
                AddedParameterType = p.ParameterType
            });
        }

        private static ToolResult<AnimationControllerResult> RemoveParameter(AnimationControllerParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationControllerResult>.Fail(
                    "Path is required for 'remove-parameter' action", ErrorCodes.INVALID_PARAM);

            if (!p.ParameterIndex.HasValue)
                return ToolResult<AnimationControllerResult>.Fail(
                    "ParameterIndex is required for 'remove-parameter' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.Path);
            if (controller == null)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"AnimatorController not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            int idx = p.ParameterIndex.Value;
            if (idx < 0 || idx >= controller.parameters.Length)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"ParameterIndex {idx} is out of range (0..{controller.parameters.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            Undo.RecordObject(controller, "Mosaic: Remove Animator Parameter");
            controller.RemoveParameter(idx);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationControllerResult>.Ok(new AnimationControllerResult
            {
                Action              = "remove-parameter",
                Path                = p.Path,
                RemovedParameterIndex = idx
            });
        }

        private static ToolResult<AnimationControllerResult> AddLayer(AnimationControllerParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationControllerResult>.Fail(
                    "Path is required for 'add-layer' action", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.LayerName))
                return ToolResult<AnimationControllerResult>.Fail(
                    "LayerName is required for 'add-layer' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.Path);
            if (controller == null)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"AnimatorController not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            Undo.RecordObject(controller, "Mosaic: Add Animator Layer");
            controller.AddLayer(p.LayerName);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationControllerResult>.Ok(new AnimationControllerResult
            {
                Action        = "add-layer",
                Path          = p.Path,
                AddedLayerName = p.LayerName
            });
        }

        private static ToolResult<AnimationControllerResult> SetLayer(AnimationControllerParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationControllerResult>.Fail(
                    "Path is required for 'set-layer' action", ErrorCodes.INVALID_PARAM);
            if (!p.LayerIndex.HasValue)
                return ToolResult<AnimationControllerResult>.Fail(
                    "LayerIndex is required for 'set-layer' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.Path);
            if (controller == null)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"AnimatorController not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            // AnimatorController.layers hands back the current state each time it's read; a
            // mutation on one of its elements is lost unless the WHOLE array is written back via
            // the setter — silently editing controller.layers[i].SomeField alone does not persist.
            var layers = controller.layers;
            int idx = p.LayerIndex.Value;
            if (idx < 0 || idx >= layers.Length)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"LayerIndex {idx} is out of range (0..{layers.Length - 1})", ErrorCodes.OUT_OF_RANGE);

            var layer = layers[idx];

            if (p.LayerWeight.HasValue) layer.defaultWeight = p.LayerWeight.Value;
            if (!string.IsNullOrEmpty(p.BlendingMode))
            {
                if (!System.Enum.TryParse<AnimatorLayerBlendingMode>(p.BlendingMode, true, out var mode))
                    return ToolResult<AnimationControllerResult>.Fail(
                        $"Unknown BlendingMode '{p.BlendingMode}'. Valid: Override, Additive", ErrorCodes.INVALID_PARAM);
                layer.blendingMode = mode;
            }
            if (p.AvatarMaskPath != null)
            {
                if (p.AvatarMaskPath.Length == 0)
                {
                    layer.avatarMask = null;
                }
                else
                {
                    var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(p.AvatarMaskPath);
                    if (mask == null)
                        return ToolResult<AnimationControllerResult>.Fail(
                            $"AvatarMask not found at '{p.AvatarMaskPath}'", ErrorCodes.NOT_FOUND);
                    layer.avatarMask = mask;
                }
            }
            if (p.IKPass.HasValue) layer.iKPass = p.IKPass.Value;
            if (p.SyncedLayerIndex.HasValue) layer.syncedLayerIndex = p.SyncedLayerIndex.Value;
            if (p.SyncedLayerAffectsTiming.HasValue) layer.syncedLayerAffectsTiming = p.SyncedLayerAffectsTiming.Value;

            Undo.RecordObject(controller, "Mosaic: Set Animator Layer");
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationControllerResult>.Ok(new AnimationControllerResult
            {
                Action                   = "set-layer",
                Path                     = p.Path,
                LayerIndex               = idx,
                LayerName                = layer.name,
                LayerWeight              = layer.defaultWeight,
                BlendingMode             = layer.blendingMode.ToString(),
                AvatarMaskPath           = layer.avatarMask != null ? AssetDatabase.GetAssetPath(layer.avatarMask) : null,
                IKPass                   = layer.iKPass,
                SyncedLayerIndex         = layer.syncedLayerIndex,
                SyncedLayerAffectsTiming = layer.syncedLayerAffectsTiming,
            });
        }

        private static ToolResult<AnimationControllerResult> RemoveLayer(AnimationControllerParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationControllerResult>.Fail(
                    "Path is required for 'remove-layer' action", ErrorCodes.INVALID_PARAM);
            if (!p.LayerIndex.HasValue)
                return ToolResult<AnimationControllerResult>.Fail(
                    "LayerIndex is required for 'remove-layer' action", ErrorCodes.INVALID_PARAM);

            var controller = AnimationToolHelpers.LoadController(p.Path);
            if (controller == null)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"AnimatorController not found at '{p.Path}'", ErrorCodes.NOT_FOUND);

            int idx = p.LayerIndex.Value;
            if (idx < 0 || idx >= controller.layers.Length)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"LayerIndex {idx} is out of range (0..{controller.layers.Length - 1})", ErrorCodes.OUT_OF_RANGE);

            string removedName = controller.layers[idx].name;

            Undo.RecordObject(controller, "Mosaic: Remove Animator Layer");
            controller.RemoveLayer(idx);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationControllerResult>.Ok(new AnimationControllerResult
            {
                Action     = "remove-layer",
                Path       = p.Path,
                LayerIndex = idx,
                LayerName  = removedName,
            });
        }

        private static ToolResult<AnimationControllerResult> CreateOverride(AnimationControllerParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AnimationControllerResult>.Fail(
                    "Path is required for 'create-override' action", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.BaseControllerPath))
                return ToolResult<AnimationControllerResult>.Fail(
                    "BaseControllerPath is required for 'create-override' action", ErrorCodes.INVALID_PARAM);

            var baseController = AnimationToolHelpers.LoadController(p.BaseControllerPath);
            if (baseController == null)
                return ToolResult<AnimationControllerResult>.Fail(
                    $"AnimatorController not found at '{p.BaseControllerPath}'", ErrorCodes.NOT_FOUND);

            var overrideController = new AnimatorOverrideController(baseController);
            // Seeded with every original clip mapped to itself — ApplyOverrides only REPLACES
            // entries in this same list, it does not add new ones, so a caller's override must
            // match one of these originals by name or path.
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(pairs);

            int overrideCount = 0;
            if (p.Overrides != null)
            {
                foreach (var ov in p.Overrides)
                {
                    if (string.IsNullOrEmpty(ov.OriginalClipName) && string.IsNullOrEmpty(ov.OriginalClipPath))
                        return ToolResult<AnimationControllerResult>.Fail(
                            "Each override needs OriginalClipName or OriginalClipPath.", ErrorCodes.INVALID_PARAM);
                    if (string.IsNullOrEmpty(ov.NewClipPath))
                        return ToolResult<AnimationControllerResult>.Fail(
                            "Each override needs NewClipPath.", ErrorCodes.INVALID_PARAM);

                    var newClip = AnimationToolHelpers.LoadClip(ov.NewClipPath, ov.NewClipName);
                    if (newClip == null)
                        return ToolResult<AnimationControllerResult>.Fail(
                            $"AnimationClip not found at '{ov.NewClipPath}'" +
                            (ov.NewClipName != null ? $" (ClipName '{ov.NewClipName}')" : ""), ErrorCodes.NOT_FOUND);

                    int idx = -1;
                    for (int i = 0; i < pairs.Count; i++)
                    {
                        var original = pairs[i].Key;
                        if (original == null) continue;
                        bool matchByName = !string.IsNullOrEmpty(ov.OriginalClipName) && original.name == ov.OriginalClipName;
                        bool matchByPath = !string.IsNullOrEmpty(ov.OriginalClipPath) &&
                            AssetDatabase.GetAssetPath(original) == ov.OriginalClipPath;
                        if (matchByName || matchByPath) { idx = i; break; }
                    }
                    if (idx < 0)
                        return ToolResult<AnimationControllerResult>.Fail(
                            $"No original clip matching '{ov.OriginalClipName ?? ov.OriginalClipPath}' found in " +
                            $"'{p.BaseControllerPath}'.", ErrorCodes.NOT_FOUND);

                    pairs[idx] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[idx].Key, newClip);
                    overrideCount++;
                }
                overrideController.ApplyOverrides(pairs);
            }

            AnimationToolHelpers.EnsureDirectoryExists(p.Path);
            AssetDatabase.CreateAsset(overrideController, p.Path);
            AssetDatabase.SaveAssets();

            return ToolResult<AnimationControllerResult>.Ok(new AnimationControllerResult
            {
                Action = "create-override",
                Path = p.Path,
                Guid = AssetDatabase.AssetPathToGUID(p.Path),
                OverrideCount = overrideCount,
            });
        }
    }
}
