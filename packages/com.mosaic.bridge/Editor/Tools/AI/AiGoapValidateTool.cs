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
    public static class AiGoapValidateTool
    {
        [MosaicTool("ai/goap-validate",
                    "Validates a GOAP agent configuration by checking goal reachability and action connectivity",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<AiGoapValidateResult> Execute(AiGoapValidateParams p)
        {
            if (string.IsNullOrWhiteSpace(p.GameObjectName))
                return ToolResult<AiGoapValidateResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<AiGoapValidateResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            // Try live component first
            var goapComponent = FindGoapComponent(go);
            if (goapComponent != null)
                return ValidateLive(goapComponent);

            // Fallback: analyze generated script
            return ValidateFromScript(go);
        }

        static MonoBehaviour FindGoapComponent(GameObject go)
        {
            foreach (var comp in go.GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;
                if (comp.GetType().Name.EndsWith("GoapAgent"))
                    return comp;
            }
            return null;
        }

        static ToolResult<AiGoapValidateResult> ValidateLive(MonoBehaviour comp)
        {
            var compType = comp.GetType();

            // Read world state, goals, actions via reflection
            var worldStateField = compType.GetField("WorldState");
            var goalsField = compType.GetField("Goals");
            var actionsField = compType.GetField("Actions");

            if (worldStateField == null || goalsField == null || actionsField == null)
                return ToolResult<AiGoapValidateResult>.Fail(
                    "GOAP component is missing required fields (WorldState, Goals, Actions)",
                    ErrorCodes.NOT_FOUND);

            var worldState = worldStateField.GetValue(comp) as Dictionary<string, object>
                ?? new Dictionary<string, object>();
            var goalsRaw = goalsField.GetValue(comp) as System.Collections.IList;
            var actionsRaw = actionsField.GetValue(comp) as System.Collections.IList;

            if (goalsRaw == null || actionsRaw == null)
                return ToolResult<AiGoapValidateResult>.Fail(
                    "GOAP component Goals or Actions is null", ErrorCodes.INTERNAL_ERROR);

            // Parse goals
            var goals = new List<(string name, Dictionary<string, object> conditions)>();
            foreach (var g in goalsRaw)
            {
                var gType = g.GetType();
                var name = gType.GetField("Name")?.GetValue(g)?.ToString() ?? "";
                var conds = gType.GetField("Conditions")?.GetValue(g) as Dictionary<string, object>
                    ?? new Dictionary<string, object>();
                goals.Add((name, conds));
            }

            // Parse actions
            var actions = new List<(string name, Dictionary<string, object> preconditions, Dictionary<string, object> effects)>();
            foreach (var a in actionsRaw)
            {
                var aType = a.GetType();
                var name = aType.GetField("Name")?.GetValue(a)?.ToString() ?? "";
                var pres = aType.GetField("Preconditions")?.GetValue(a) as Dictionary<string, object>
                    ?? new Dictionary<string, object>();
                var effs = aType.GetField("Effects")?.GetValue(a) as Dictionary<string, object>
                    ?? new Dictionary<string, object>();
                actions.Add((name, pres, effs));
            }

            return PerformValidation(worldState, goals, actions);
        }

        static ToolResult<AiGoapValidateResult> ValidateFromScript(GameObject go)
        {
            var genPath = "Assets/Generated/AI/";
            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullGenPath = Path.Combine(projectRoot, genPath);

            if (!Directory.Exists(fullGenPath))
                return ToolResult<AiGoapValidateResult>.Fail(
                    $"No GOAP component found on '{go.name}' and no generated scripts found",
                    ErrorCodes.NOT_FOUND);

            var scripts = Directory.GetFiles(fullGenPath, "*GoapAgent.cs");
            if (scripts.Length == 0)
                return ToolResult<AiGoapValidateResult>.Fail(
                    $"No GOAP component found on '{go.name}' and no generated GOAP scripts found",
                    ErrorCodes.NOT_FOUND);

            var content = File.ReadAllText(scripts[0]);

            // Parse world state from script
            var worldState = new Dictionary<string, object>();
            var wsMatches = Regex.Matches(content, @"WorldState\[""([^""]+)""\]\s*=\s*(.+);");
            foreach (Match m in wsMatches)
            {
                var key = m.Groups[1].Value;
                var valStr = m.Groups[2].Value.Trim();
                worldState[key] = ParseScriptValue(valStr);
            }

            // Parse goals — look for GoapGoal blocks
            var goals = new List<(string name, Dictionary<string, object> conditions)>();
            var goalBlocks = Regex.Matches(content,
                @"Goals\.Add\(new GoapGoal\s*\{([\s\S]*?)\}\);",
                RegexOptions.Multiline);
            foreach (Match gb in goalBlocks)
            {
                var block = gb.Groups[1].Value;
                var nameMatch = Regex.Match(block, @"Name\s*=\s*""([^""]+)""");
                var goalName = nameMatch.Success ? nameMatch.Groups[1].Value : "";
                var conds = ParseConditionsBlock(block);
                goals.Add((goalName, conds));
            }

            // Parse actions
            var actions = new List<(string name, Dictionary<string, object> preconditions, Dictionary<string, object> effects)>();
            var actionBlocks = Regex.Matches(content,
                @"Actions\.Add\(new GoapAction\s*\{([\s\S]*?)\}\);",
                RegexOptions.Multiline);
            foreach (Match ab in actionBlocks)
            {
                var block = ab.Groups[1].Value;
                var nameMatch = Regex.Match(block, @"Name\s*=\s*""([^""]+)""");
                var actionName = nameMatch.Success ? nameMatch.Groups[1].Value : "";

                var preBlock = ExtractDictionaryBlock(block, "Preconditions");
                var effBlock = ExtractDictionaryBlock(block, "Effects");

                actions.Add((actionName, preBlock, effBlock));
            }

            return PerformValidation(worldState, goals, actions);
        }

        internal static ToolResult<AiGoapValidateResult> PerformValidation(
            Dictionary<string, object> worldState,
            List<(string name, Dictionary<string, object> conditions)> goals,
            List<(string name, Dictionary<string, object> preconditions, Dictionary<string, object> effects)> actions)
        {
            // Compute all achievable effects: start with world state keys, then forward-chain
            var achievableState = new Dictionary<string, HashSet<object>>();
            foreach (var kv in worldState)
            {
                if (!achievableState.ContainsKey(kv.Key))
                    achievableState[kv.Key] = new HashSet<object>();
                achievableState[kv.Key].Add(kv.Value);
            }

            // Forward-chain: iteratively apply actions whose preconditions are met
            bool changed = true;
            int maxIterations = actions.Count * 2 + 1;
            int iteration = 0;
            while (changed && iteration < maxIterations)
            {
                changed = false;
                iteration++;
                foreach (var action in actions)
                {
                    if (PreconditionsAchievable(action.preconditions, achievableState))
                    {
                        foreach (var eff in action.effects)
                        {
                            if (!achievableState.ContainsKey(eff.Key))
                                achievableState[eff.Key] = new HashSet<object>();
                            if (achievableState[eff.Key].Add(eff.Value))
                                changed = true;
                        }
                    }
                }
            }

            // Check each goal
            var achievableGoals = new List<string>();
            var unachievableGoals = new List<UnachievableGoalInfo>();

            foreach (var goal in goals)
            {
                bool achievable = true;
                string missingEffect = null;

                foreach (var cond in goal.conditions)
                {
                    if (!achievableState.ContainsKey(cond.Key) ||
                        !achievableState[cond.Key].Contains(cond.Value))
                    {
                        achievable = false;
                        missingEffect = $"{cond.Key}={cond.Value}";
                        break;
                    }
                }

                if (achievable)
                    achievableGoals.Add(goal.name);
                else
                    unachievableGoals.Add(new UnachievableGoalInfo
                    {
                        Goal = goal.name,
                        MissingEffect = missingEffect
                    });
            }

            // Find orphaned actions (preconditions can never be met)
            var orphanedActions = new List<string>();
            foreach (var action in actions)
            {
                if (!PreconditionsAchievable(action.preconditions, achievableState))
                    orphanedActions.Add(action.name);
            }

            bool isValid = unachievableGoals.Count == 0 && orphanedActions.Count == 0;

            return ToolResult<AiGoapValidateResult>.Ok(new AiGoapValidateResult
            {
                AchievableGoals = achievableGoals.ToArray(),
                UnachievableGoals = unachievableGoals.ToArray(),
                OrphanedActions = orphanedActions.ToArray(),
                IsValid = isValid
            });
        }

        static bool PreconditionsAchievable(Dictionary<string, object> preconditions,
            Dictionary<string, HashSet<object>> achievableState)
        {
            foreach (var pre in preconditions)
            {
                if (!achievableState.ContainsKey(pre.Key) ||
                    !achievableState[pre.Key].Contains(pre.Value))
                    return false;
            }
            return true;
        }

        static object ParseScriptValue(string valStr)
        {
            valStr = valStr.Trim();
            if (valStr == "true") return true;
            if (valStr == "false") return false;
            if (valStr.EndsWith("f") && float.TryParse(valStr.TrimEnd('f'), out var fv)) return fv;
            if (int.TryParse(valStr, out var iv)) return iv;
            // Strip quotes
            if (valStr.StartsWith("\"") && valStr.EndsWith("\""))
                return valStr.Substring(1, valStr.Length - 2);
            return valStr;
        }

        static Dictionary<string, object> ParseConditionsBlock(string block)
        {
            var result = new Dictionary<string, object>();
            var matches = Regex.Matches(block, @"\{\s*""([^""]+)""\s*,\s*(.+?)\s*\}");
            foreach (Match m in matches)
            {
                result[m.Groups[1].Value] = ParseScriptValue(m.Groups[2].Value.TrimEnd(','));
            }
            return result;
        }

        static Dictionary<string, object> ExtractDictionaryBlock(string block, string fieldName)
        {
            var result = new Dictionary<string, object>();
            var pattern = fieldName + @"\s*=\s*new Dictionary<string, object>\s*\{([\s\S]*?)\}";
            var match = Regex.Match(block, pattern);
            if (match.Success)
            {
                var inner = match.Groups[1].Value;
                var entries = Regex.Matches(inner, @"\{\s*""([^""]+)""\s*,\s*(.+?)\s*\}");
                foreach (Match e in entries)
                {
                    result[e.Groups[1].Value] = ParseScriptValue(e.Groups[2].Value.TrimEnd(','));
                }
            }
            return result;
        }
    }
}
