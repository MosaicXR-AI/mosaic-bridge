#if MOSAIC_HAS_CINEMACHINE
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public static class CinemachineCreateManagerTool
    {
        [MosaicTool("cinemachine/create-manager",
                    "Creates a manager camera (StateDriven, Mixing, Sequencer, ClearShot) and " +
                    "reparents ChildVCamNames under it — a manager only sees its own hierarchy " +
                    "children. StateDriven: AnimatedTargetName + LayerIndex + StateDrivenInstructions " +
                    "(StateName 'Layer.State', hashed via Animator.StringToHash, + CameraIndex). " +
                    "Sequencer: Loop + SequencerInstructions (CameraIndex, Hold, blend). ClearShot: " +
                    "ActivateAfter/MinDuration/RandomizeChoice (automatic best shot via child shot-" +
                    "evaluator extensions). Mixing: reparents only — weights are set at runtime.",
                    isReadOnly: false)]
        public static ToolResult<CinemachineCreateManagerResult> Execute(CinemachineCreateManagerParams p)
        {
            if (p.ChildVCamNames == null || p.ChildVCamNames.Length == 0)
                return ToolResult<CinemachineCreateManagerResult>.Fail(
                    "ChildVCamNames must contain at least one vcam name", ErrorCodes.INVALID_PARAM);

            var childVCams = new CinemachineVirtualCameraBase[p.ChildVCamNames.Length];
            var childGos = new GameObject[p.ChildVCamNames.Length];
            for (int i = 0; i < p.ChildVCamNames.Length; i++)
            {
                var childGo = GameObject.Find(p.ChildVCamNames[i]);
                if (childGo == null)
                    return ToolResult<CinemachineCreateManagerResult>.Fail(
                        $"GameObject '{p.ChildVCamNames[i]}' not found", ErrorCodes.NOT_FOUND);
                var childVCam = childGo.GetComponent<CinemachineVirtualCameraBase>();
                if (childVCam == null)
                    return ToolResult<CinemachineCreateManagerResult>.Fail(
                        $"'{p.ChildVCamNames[i]}' has no Cinemachine virtual camera component", ErrorCodes.INVALID_PARAM);
                childGos[i] = childGo;
                childVCams[i] = childVCam;
            }

            var go = new GameObject(p.Name);
            int instructionCount = 0;

            switch (p.ManagerType?.ToLowerInvariant())
            {
                case "statedriven":
                {
                    var stateDriven = go.AddComponent<CinemachineStateDrivenCamera>();
                    if (!string.IsNullOrEmpty(p.AnimatedTargetName))
                    {
                        var animatedGo = GameObject.Find(p.AnimatedTargetName);
                        if (animatedGo == null)
                            return Fail(go, $"GameObject '{p.AnimatedTargetName}' not found", ErrorCodes.NOT_FOUND);
                        var animator = animatedGo.GetComponent<Animator>();
                        if (animator == null)
                            return Fail(go, $"'{p.AnimatedTargetName}' has no Animator component", ErrorCodes.NOT_FOUND);
                        stateDriven.AnimatedTarget = animator;
                    }
                    stateDriven.LayerIndex = p.LayerIndex;

                    if (p.StateDrivenInstructions != null)
                    {
                        var instructions = new CinemachineStateDrivenCamera.Instruction[p.StateDrivenInstructions.Length];
                        for (int i = 0; i < p.StateDrivenInstructions.Length; i++)
                        {
                            var input = p.StateDrivenInstructions[i];
                            if (input.CameraIndex < 0 || input.CameraIndex >= childVCams.Length)
                                return Fail(go, $"StateDrivenInstructions[{i}].CameraIndex {input.CameraIndex} is out of range (0..{childVCams.Length - 1})",
                                    ErrorCodes.OUT_OF_RANGE);
                            instructions[i] = new CinemachineStateDrivenCamera.Instruction
                            {
                                FullHash = Animator.StringToHash(input.StateName),
                                Camera = childVCams[input.CameraIndex],
                                ActivateAfter = input.ActivateAfter,
                                MinDuration = input.MinDuration,
                            };
                        }
                        stateDriven.Instructions = instructions;
                        instructionCount = instructions.Length;
                    }
                    break;
                }
                case "mixing":
                    go.AddComponent<CinemachineMixingCamera>();
                    break;
                case "sequencer":
                {
                    var sequencer = go.AddComponent<CinemachineSequencerCamera>();
                    if (p.Loop.HasValue) sequencer.Loop = p.Loop.Value;

                    if (p.SequencerInstructions != null)
                    {
                        var instructions = new System.Collections.Generic.List<CinemachineSequencerCamera.Instruction>();
                        for (int i = 0; i < p.SequencerInstructions.Length; i++)
                        {
                            var input = p.SequencerInstructions[i];
                            if (input.CameraIndex < 0 || input.CameraIndex >= childVCams.Length)
                                return Fail(go, $"SequencerInstructions[{i}].CameraIndex {input.CameraIndex} is out of range (0..{childVCams.Length - 1})",
                                    ErrorCodes.OUT_OF_RANGE);
                            if (!TryParseBlendStyle(input.BlendType, out var style))
                                return Fail(go, $"Invalid BlendType '{input.BlendType}' in SequencerInstructions[{i}]. Valid: Cut, EaseInOut, Linear",
                                    ErrorCodes.INVALID_PARAM);
                            instructions.Add(new CinemachineSequencerCamera.Instruction
                            {
                                Camera = childVCams[input.CameraIndex],
                                Hold = input.Hold,
                                Blend = new CinemachineBlendDefinition(style, input.BlendTime),
                            });
                        }
                        sequencer.Instructions = instructions;
                        instructionCount = instructions.Count;
                    }
                    break;
                }
                case "clearshot":
                {
                    var clearShot = go.AddComponent<CinemachineClearShot>();
                    if (p.ActivateAfter.HasValue) clearShot.ActivateAfter = p.ActivateAfter.Value;
                    if (p.MinDuration.HasValue) clearShot.MinDuration = p.MinDuration.Value;
                    if (p.RandomizeChoice.HasValue) clearShot.RandomizeChoice = p.RandomizeChoice.Value;
                    break;
                }
                default:
                    Object.DestroyImmediate(go);
                    return ToolResult<CinemachineCreateManagerResult>.Fail(
                        $"Unknown ManagerType '{p.ManagerType}'. Valid: StateDriven, Mixing, Sequencer, ClearShot",
                        ErrorCodes.INVALID_PARAM);
            }

            foreach (var childGo in childGos)
                Undo.SetTransformParent(childGo.transform, go.transform, "Mosaic: Reparent Under Cinemachine Manager");

            Undo.RegisterCreatedObjectUndo(go, "Mosaic: Create Cinemachine Manager");

            return ToolResult<CinemachineCreateManagerResult>.Ok(new CinemachineCreateManagerResult
            {
                InstanceId = UnityIds.Of(go),
                Name = go.name,
                ManagerType = p.ManagerType,
                ChildCount = childGos.Length,
                InstructionCount = instructionCount,
            });
        }

        private static ToolResult<CinemachineCreateManagerResult> Fail(GameObject toDestroy, string message, string code)
        {
            Object.DestroyImmediate(toDestroy);
            return ToolResult<CinemachineCreateManagerResult>.Fail(message, code);
        }

        private static bool TryParseBlendStyle(string value, out CinemachineBlendDefinition.Styles result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "cut":       result = CinemachineBlendDefinition.Styles.Cut;       return true;
                case "easeinout": result = CinemachineBlendDefinition.Styles.EaseInOut; return true;
                case "linear":    result = CinemachineBlendDefinition.Styles.Linear;    return true;
                default:          result = CinemachineBlendDefinition.Styles.EaseInOut; return false;
            }
        }
    }
}
#endif
