using System.Collections.Generic;

namespace Mosaic.Bridge.Contracts.Interfaces
{
    /// <summary>
    /// Domain knowledge base query interface.
    /// Per FR28-FR33: the knowledge base provides physically accurate values
    /// (physics constants, PBR materials, etc.) for tools to query at runtime.
    /// </summary>
    /// <remarks>
    /// CRITICAL: Per FR30, this interface NEVER throws on missing keys and NEVER hallucinates values.
    /// It returns null or an empty result for missing entries. Tools that need a value not in the
    /// knowledge base MUST surface an explicit "no data available" warning (per FR31).
    /// </remarks>
    public interface IKnowledgeProvider
    {
        /// <summary>
        /// Get a numeric constant by key (e.g., "gravity_earth" returns 9.80665).
        /// Returns null if the key is not in the knowledge base.
        /// </summary>
        double? GetConstant(string key);

        /// <summary>
        /// Get a material by name (e.g., "wood_oak" returns its PBR properties).
        /// Returns null if the material is not in the knowledge base.
        /// </summary>
        MaterialInfo GetMaterial(string materialName);

        /// <summary>
        /// Find materials matching the given filter predicate.
        /// Returns an empty list if no materials match (never null).
        /// </summary>
        IReadOnlyList<MaterialInfo> FindMaterials(System.Func<MaterialInfo, bool> filter);

        /// <summary>
        /// Get a formula by name (e.g., "buoyancy" returns its description, parameters, and Unity tip).
        /// Returns null if the formula is not in the knowledge base.
        /// </summary>
        FormulaInfo GetFormula(string formulaName);

        /// <summary>
        /// Get a category-scoped knowledge base context as formatted text for AI system prompts.
        /// Returns an empty string if the category has no knowledge base entries.
        /// </summary>
        string GetContextForCategory(string category);

        /// <summary>
        /// Check whether a specific knowledge base entry is available.
        /// </summary>
        bool HasEntry(string entryId);
    }

    /// <summary>
    /// Material information from the PBR materials knowledge base.
    /// Per FR33: every entry has value, unit (where applicable), source, description,
    /// and optional fields for assumptions, unityNotes, and tradeoffs.
    /// </summary>
    public class MaterialInfo
    {
        public string Name { get; set; }
        public float[] Albedo { get; set; }
        public float Roughness { get; set; }
        public float Metalness { get; set; }
        public float? Ior { get; set; }
        public float? DensityKgPerM3 { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public string Assumptions { get; set; }
        public string UnityNotes { get; set; }
        public string Tradeoffs { get; set; }
    }

    /// <summary>
    /// Formula information from the physics formulas knowledge base.
    /// </summary>
    public class FormulaInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Formula { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
        public string UnityTip { get; set; }
        public string Source { get; set; }
    }
}
