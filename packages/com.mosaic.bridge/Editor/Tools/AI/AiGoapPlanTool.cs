using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AI
{
    public static class AiGoapPlanTool
    {
        [MosaicTool("ai/goap-plan",
                    "Runs the GOAP planner on an existing GOAP agent and returns the computed plan",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<AiGoapPlanResult> Execute(AiGoapPlanParams p)
        {
            if (string.IsNullOrWhiteSpace(p.GameObjectName))
                return ToolResult<AiGoapPlanResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var maxDepth = p.MaxPlanDepth > 0 ? p.MaxPlanDepth : 10;

            // Try to find a live GOAP component on the GO
            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<AiGoapPlanResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            // Look for a component ending in "GoapAgent"
            var goapComponent = FindGoapComponent(go);
            if (goapComponent != null)
            {
                return RunLivePlan(goapComponent, maxDepth);
            }

            // Fallback: analyze the script file for a static plan
            return AnalyzeScriptPlan(go, maxDepth);
        }

        static MonoBehaviour FindGoapComponent(GameObject go)
        {
            foreach (var comp in go.GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;
                var typeName = comp.GetType().Name;
                if (typeName.EndsWith("GoapAgent"))
                    return comp;
            }
            return null;
        }

        static ToolResult<AiGoapPlanResult> RunLivePlan(MonoBehaviour comp, int maxDepth)
        {
            var compType = comp.GetType();

            // Invoke RunPlanner via reflection
            var plannerMethod = compType.GetMethod("RunPlanner",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (plannerMethod == null)
                return ToolResult<AiGoapPlanResult>.Fail(
                    "GOAP component does not have a RunPlanner method", ErrorCodes.NOT_FOUND);

            try
            {
                object result;
                var methodParams = plannerMethod.GetParameters();
                if (methodParams.Length == 1)
                    result = plannerMethod.Invoke(comp, new object[] { maxDepth });
                else
                    result = plannerMethod.Invoke(comp, null);

                // result is List<GoapAction>; read via reflection
                var planList = result as System.Collections.IList;
                if (planList == null || planList.Count == 0)
                {
                    return ToolResult<AiGoapPlanResult>.Ok(new AiGoapPlanResult
                    {
                        Plan = new PlanStep[0],
                        PlanCost = 0f,
                        GoalSelected = "",
                        Success = false
                    });
                }

                var steps = new List<PlanStep>();
                float totalCost = 0f;
                foreach (var action in planList)
                {
                    var actionType = action.GetType();
                    var nameField = actionType.GetField("Name");
                    var costField = actionType.GetField("Cost");
                    var effectsField = actionType.GetField("Effects");

                    string actionName = nameField?.GetValue(action)?.ToString() ?? "Unknown";
                    float cost = costField != null ? (float)costField.GetValue(action) : 0f;
                    string effectsStr = "";

                    if (effectsField != null)
                    {
                        var effects = effectsField.GetValue(action) as IDictionary<string, object>;
                        if (effects != null)
                            effectsStr = string.Join(", ", effects.Select(kv => $"{kv.Key}={kv.Value}"));
                    }

                    totalCost += cost;
                    steps.Add(new PlanStep
                    {
                        ActionName = actionName,
                        ResultingState = effectsStr
                    });
                }

                // Determine selected goal (first goal in the sorted list)
                var goalsField = comp.GetType().GetField("Goals");
                string goalSelected = "";
                if (goalsField != null)
                {
                    var goals = goalsField.GetValue(comp) as System.Collections.IList;
                    if (goals != null && goals.Count > 0)
                    {
                        var firstGoal = goals[0];
                        var goalNameField = firstGoal.GetType().GetField("Name");
                        goalSelected = goalNameField?.GetValue(firstGoal)?.ToString() ?? "";
                    }
                }

                return ToolResult<AiGoapPlanResult>.Ok(new AiGoapPlanResult
                {
                    Plan = steps.ToArray(),
                    PlanCost = totalCost,
                    GoalSelected = goalSelected,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                return ToolResult<AiGoapPlanResult>.Fail(
                    $"Planner execution failed: {ex.Message}", ErrorCodes.INTERNAL_ERROR);
            }
        }

        static ToolResult<AiGoapPlanResult> AnalyzeScriptPlan(GameObject go, int maxDepth)
        {
            // Look for generated GOAP script files
            var genPath = "Assets/Generated/AI/";
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullGenPath = Path.Combine(projectRoot, genPath);

            if (!Directory.Exists(fullGenPath))
                return ToolResult<AiGoapPlanResult>.Fail(
                    $"No GOAP component found on '{go.name}' and no generated scripts found",
                    ErrorCodes.NOT_FOUND);

            // Find script files matching *GoapAgent.cs
            var scripts = Directory.GetFiles(fullGenPath, "*GoapAgent.cs");
            if (scripts.Length == 0)
                return ToolResult<AiGoapPlanResult>.Fail(
                    $"No GOAP component found on '{go.name}' and no generated GOAP scripts found",
                    ErrorCodes.NOT_FOUND);

            // Parse the first matching script for plan info
            var scriptContent = File.ReadAllText(scripts[0]);

            // Extract action names from the script
            var actionMatches = Regex.Matches(scriptContent, @"Name\s*=\s*""([^""]+)""");
            var planSteps = new List<PlanStep>();
            foreach (Match m in actionMatches)
            {
                planSteps.Add(new PlanStep
                {
                    ActionName = m.Groups[1].Value,
                    ResultingState = "(static analysis - script not yet compiled)"
                });
            }

            return ToolResult<AiGoapPlanResult>.Ok(new AiGoapPlanResult
            {
                Plan = planSteps.ToArray(),
                PlanCost = 0f,
                GoalSelected = "(static analysis)",
                Success = planSteps.Count > 0
            });
        }
    }
}
