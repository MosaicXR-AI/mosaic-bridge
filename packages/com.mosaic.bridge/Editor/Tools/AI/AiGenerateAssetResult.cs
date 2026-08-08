namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiGenerateAssetResult
    {
        /// <summary>
        /// "unity-mcp" when Unity AI is present and the caller should execute the request
        /// through Unity's official asset-generation MCP, or "handoff" when it is not and a
        /// human must generate the asset.
        /// </summary>
        public string Mode { get; set; }

        /// <summary>Whether Unity AI generation was detected in this Editor.</summary>
        public bool UnityAiAvailable { get; set; }

        /// <summary>What the probe matched, for diagnostics. Null when unavailable.</summary>
        public string DetectedVia { get; set; }

        /// <summary>Unity AI command for this asset type, e.g. "GenerateMaterial".</summary>
        public string Command { get; set; }

        /// <summary>
        /// Advisory model ids. The real catalog is account- and entitlement-scoped and
        /// changes, so confirm against Unity's model-listing tool before use.
        /// </summary>
        public string[] SuggestedModels { get; set; }

        /// <summary>The final prompt, including any knowledge-base grounding.</summary>
        public string Prompt { get; set; }

        /// <summary>Project-relative path the generated asset should be written to.</summary>
        public string SavePath { get; set; }

        public int? Width { get; set; }
        public int? Height { get; set; }
        public float? DurationSeconds { get; set; }

        /// <summary>
        /// Measured values injected into the prompt, with their source, so the numbers are
        /// auditable rather than invented. Empty when nothing matched.
        /// </summary>
        public string[] KnowledgeApplied { get; set; }

        /// <summary>Plain-language instruction for the caller on what to do next.</summary>
        public string NextStep { get; set; }

        /// <summary>True when generation will consume the user's Unity AI credits.</summary>
        public bool ConsumesCredits { get; set; }
    }
}
