using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Animations
{
    public static class AnimationControllerTool
    {
        private const string ValidActions = "create, info, add-parameter, remove-parameter, add-layer";

        [MosaicTool("animation/controller",
                    "Manages AnimatorController assets: create, inspect, add/remove parameters, add layers",
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
    }
}
