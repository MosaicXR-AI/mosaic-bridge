using System;
using System.Globalization;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Diagnostics;
using Mosaic.Bridge.Core.Server;

namespace Mosaic.Bridge.Core.Pipeline.Stages
{
    /// <summary>
    /// Pre-execution pipeline stage that proactively suggests domain-correct values
    /// from the knowledge base. Adds informational warnings to the execution context
    /// but never blocks the pipeline (always returns true).
    /// </summary>
    /// <remarks>
    /// Story 12.3 — Knowledge Advisor Stage.
    /// - For material tools: looks up PBR-correct values (roughness, metalness, albedo).
    /// - For component tools setting physics properties: looks up physics material density for mass suggestions.
    /// - Only runs when Mode >= Validated. Direct mode is skipped entirely.
    /// - If the knowledge base returns null (no data), the stage does nothing. Surfacing
    ///   "no data available" warnings is Story 5.4's responsibility, not this stage's.
    /// </remarks>
    public sealed class KnowledgeAdvisorStage : IPipelineStage
    {
        private readonly IMosaicLogger _logger;

        public KnowledgeAdvisorStage(IMosaicLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
        {
            // Skip for Direct mode — no advisory overhead
            if (context.Mode < ExecutionMode.Validated)
                return true;

            try
            {
                var category = ResolveCategory(context);
                if (string.IsNullOrEmpty(category))
                    return true;

                // Story 10.2: Record KB query for telemetry
                UsageTelemetry.RecordKbQuery(category);

                switch (category.ToLowerInvariant())
                {
                    case "material":
                        AdviseMaterialTool(context);
                        break;
                    case "component":
                        AdviseComponentTool(context);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Knowledge advisor must never crash the pipeline
                _logger.Warn("KnowledgeAdvisorStage encountered an error; skipping advice.",
                    ("error", ex.Message));
            }

            // Always continue — this stage is purely advisory
            return true;
        }

        /// <summary>
        /// For material/create and material/set-property tools, looks up the material name
        /// in the PBR knowledge base and suggests correct values.
        /// </summary>
        private void AdviseMaterialTool(ExecutionContext context)
        {
            if (context.Parameters == null)
                return;

            var materialName = ExtractMaterialName(context);
            if (string.IsNullOrEmpty(materialName))
                return;

            var provider = ResolveKnowledgeProvider();
            if (provider == null)
                return;

            var info = provider.GetMaterial(materialName);
            if (info == null)
                return;

            // Record the KB reference
            context.KBReferences.Add($"pbr:{info.Name}");

            // Build suggestion text
            var suggestion = string.Format(
                CultureInfo.InvariantCulture,
                "[KB] PBR reference for \"{0}\": roughness={1:F2}, metalness={2:F2}",
                info.Name,
                info.Roughness,
                info.Metalness);

            if (info.Albedo != null && info.Albedo.Length >= 3)
            {
                suggestion += string.Format(
                    CultureInfo.InvariantCulture,
                    ", albedo=({0:F2}, {1:F2}, {2:F2})",
                    info.Albedo[0], info.Albedo[1], info.Albedo[2]);
            }

            if (!string.IsNullOrEmpty(info.Source))
            {
                suggestion += $" [source: {info.Source}]";
            }

            context.Warnings.Add(suggestion);

            _logger.Debug("KnowledgeAdvisor: PBR suggestion added for material.",
                ("material", materialName));
        }

        /// <summary>
        /// For component tools setting physics properties (e.g., mass on a Rigidbody),
        /// looks up the physics material density and suggests a realistic mass.
        /// </summary>
        private void AdviseComponentTool(ExecutionContext context)
        {
            if (context.Parameters == null)
                return;

            // Only advise when setting properties (component/set_property or component/set-property)
            var toolName = context.ToolName ?? string.Empty;
            if (!toolName.Contains("set_property") && !toolName.Contains("set-property"))
                return;

            // Check if the property being set is "mass"
            var propertyName = context.Parameters.Value<string>("propertyName")
                               ?? context.Parameters.Value<string>("property");
            if (propertyName == null ||
                !string.Equals(propertyName, "mass", StringComparison.OrdinalIgnoreCase))
                return;

            // Try to find a material name hint in the parameters
            var materialHint = context.Parameters.Value<string>("materialHint")
                               ?? context.Parameters.Value<string>("material")
                               ?? context.Parameters.Value<string>("physicsMaterial");
            if (string.IsNullOrEmpty(materialHint))
                return;

            var provider = ResolveKnowledgeProvider();
            if (provider == null)
                return;

            var info = provider.GetMaterial(materialHint);
            if (info == null || !info.DensityKgPerM3.HasValue)
                return;

            context.KBReferences.Add($"physics:{info.Name}");

            var suggestion = string.Format(
                CultureInfo.InvariantCulture,
                "[KB] Physics reference for \"{0}\": density={1:F1} kg/m\u00B3. " +
                "For a 1m\u00B3 object, realistic mass \u2248 {1:F1} kg.",
                info.Name,
                info.DensityKgPerM3.Value);

            context.Warnings.Add(suggestion);

            _logger.Debug("KnowledgeAdvisor: physics mass suggestion added.",
                ("material", materialHint),
                ("density", info.DensityKgPerM3.Value));
        }

        /// <summary>
        /// Extracts a material name from the tool parameters. Checks common parameter keys.
        /// </summary>
        private static string ExtractMaterialName(ExecutionContext context)
        {
            var parameters = context.Parameters;
            if (parameters == null)
                return null;

            // Try common parameter names for material identification
            return parameters.Value<string>("materialName")
                   ?? parameters.Value<string>("name")
                   ?? parameters.Value<string>("material");
        }

        /// <summary>
        /// Resolves the tool category from the registry entry, or parses it from the tool name.
        /// Follows the same convention as SemanticValidatorStage.
        /// </summary>
        private static string ResolveCategory(ExecutionContext context)
        {
            if (context.ToolEntry?.Category != null)
                return context.ToolEntry.Category;

            if (string.IsNullOrEmpty(context.ToolName))
                return null;

            // Format: "mosaic_category_action" — split on '_' and take the second segment
            var parts = context.ToolName.Split('_');
            return parts.Length >= 2 ? parts[1] : context.ToolName;
        }

        /// <summary>
        /// Resolves the IKnowledgeProvider from the service locator.
        /// Returns null if unavailable (e.g., in test context without Unity assets).
        /// </summary>
        private static IKnowledgeProvider ResolveKnowledgeProvider()
        {
            // NOTE: In production, this will be resolved via the service container
            // registered during Bootstrap. For now, we use a static accessor pattern
            // consistent with other pipeline stages. When the DI container (Story 2.x)
            // lands, this will be injected via constructor.
            return KnowledgeProviderAccessor.Current;
        }
    }

    /// <summary>
    /// Static accessor for the knowledge provider, allowing pipeline stages to
    /// resolve it without constructor injection until the DI container is available.
    /// Test code can set this to a mock/stub.
    /// </summary>
    public static class KnowledgeProviderAccessor
    {
        /// <summary>
        /// The current knowledge provider instance. Null if not initialized.
        /// Set during Bootstrap; can be overridden in tests.
        /// </summary>
        public static IKnowledgeProvider Current { get; set; }
    }
}
