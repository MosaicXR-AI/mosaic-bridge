using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AI
{
    public static class AiUtilityEvaluateTool
    {
        [MosaicTool("ai/utility-evaluate",
                    "Evaluates a utility AI component on a GameObject and returns action scores with per-consideration breakdowns",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<AiUtilityEvaluateResult> Execute(AiUtilityEvaluateParams p)
        {
            if (string.IsNullOrWhiteSpace(p.GameObjectName))
                return ToolResult<AiUtilityEvaluateResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<AiUtilityEvaluateResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            // Find a MonoBehaviour whose type name ends with "UtilityAI"
            MonoBehaviour uaiComponent = null;
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name.EndsWith("UtilityAI"))
                {
                    uaiComponent = mb;
                    break;
                }
            }

            if (uaiComponent == null)
                return ToolResult<AiUtilityEvaluateResult>.Fail(
                    $"No UtilityAI component found on '{p.GameObjectName}'. " +
                    "The generated script may not be compiled yet — enter Play mode or trigger a domain reload first.",
                    ErrorCodes.NOT_FOUND);

            var uaiType = uaiComponent.GetType();

            // Apply input overrides
            if (p.InputOverrides != null)
            {
                foreach (var ov in p.InputOverrides)
                {
                    var field = uaiType.GetField(ov.Name, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(float))
                        field.SetValue(uaiComponent, ov.Value);
                }
            }

            // Call Score_ methods via reflection
            var scoreMethods = uaiType
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name.StartsWith("Score_") && m.GetParameters().Length == 0)
                .ToList();

            if (scoreMethods.Count == 0)
                return ToolResult<AiUtilityEvaluateResult>.Fail(
                    "No Score_ methods found on the UtilityAI component. Script may not be compiled.",
                    ErrorCodes.NOT_FOUND);

            var scores = new List<ActionScore>();
            float bestScore = float.MinValue;
            string bestAction = null;

            foreach (var method in scoreMethods)
            {
                var actionName = method.Name.Substring("Score_".Length);
                float score;
                try
                {
                    score = (float)method.Invoke(uaiComponent, null);
                }
                catch (Exception ex)
                {
                    score = 0f;
                    Debug.LogWarning($"[UtilityAI Evaluate] Error invoking {method.Name}: {ex.Message}");
                }

                scores.Add(new ActionScore
                {
                    Action         = actionName,
                    Score          = score,
                    Considerations = new List<ConsiderationScore>() // Per-consideration breakdown requires runtime introspection not available via reflection
                });

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAction = actionName;
                }
            }

            return ToolResult<AiUtilityEvaluateResult>.Ok(new AiUtilityEvaluateResult
            {
                SelectedAction = bestAction,
                Scores         = scores
            });
        }
    }
}
