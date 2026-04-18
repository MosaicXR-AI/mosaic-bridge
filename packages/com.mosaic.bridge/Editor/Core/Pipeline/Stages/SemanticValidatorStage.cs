using System;
using System.Collections.Generic;
using Mosaic.Bridge.Core.Pipeline.Validation;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Core.Pipeline.Stages
{
    /// <summary>
    /// Pre-execution pipeline stage that evaluates semantic validation rules
    /// against tool parameters. Warnings are accumulated on the context;
    /// errors abort the pipeline with a 400 response.
    /// </summary>
    public sealed class SemanticValidatorStage : IPipelineStage
    {
        private readonly List<IValidationRule> _rules;

        public SemanticValidatorStage(IEnumerable<IValidationRule> rules)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            _rules = new List<IValidationRule>(rules);
        }

        /// <summary>
        /// Convenience constructor that registers all built-in validation rules.
        /// </summary>
        public SemanticValidatorStage()
            : this(new IValidationRule[]
            {
                new TransformRangeRule(),
                new PbrRangeRule(),
                new DuplicateComponentRule(),
                new ScriptExistsRule()
            })
        {
        }

        public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
        {
            var toolCategory = ResolveCategory(context);

            foreach (var rule in _rules)
            {
                // Apply rules that match the tool's category, or rules that apply to all tools
                if (rule.Category != null &&
                    !string.Equals(rule.Category, toolCategory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ValidationResult result;
                try
                {
                    result = rule.Validate(context);
                }
                catch (Exception ex)
                {
                    // Validation rules should never crash the pipeline; treat exceptions as warnings
                    context.Warnings.Add(
                        $"Validation rule {rule.GetType().Name} threw an exception: {ex.Message}");
                    continue;
                }

                if (result == null || result.IsValid)
                {
                    // Pass or warning
                    if (result?.Message != null)
                    {
                        context.Warnings.Add(result.Message);
                    }
                    continue;
                }

                // Error — abort the pipeline
                toolResult = new HandlerResponse
                {
                    StatusCode = 400,
                    ContentType = "application/json",
                    Body = JsonConvert.SerializeObject(new
                    {
                        error = "VALIDATION_ERROR",
                        message = result.Message ?? "Semantic validation failed.",
                        rule = rule.GetType().Name
                    })
                };
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves the tool category from the registry entry, or parses it from the tool name.
        /// Tool names follow the pattern "mosaic_{category}_{action}" (e.g., "mosaic_gameobject_create").
        /// </summary>
        private static string ResolveCategory(ExecutionContext context)
        {
            // Prefer the registry entry's authoritative category
            if (context.ToolEntry?.Category != null)
                return context.ToolEntry.Category;

            // Fall back to parsing from tool name
            if (string.IsNullOrEmpty(context.ToolName))
                return null;

            // Format: "mosaic_category_action" — split on '_' and take the second segment
            var parts = context.ToolName.Split('_');
            if (parts.Length >= 2)
                return parts[1];

            return context.ToolName;
        }
    }
}
