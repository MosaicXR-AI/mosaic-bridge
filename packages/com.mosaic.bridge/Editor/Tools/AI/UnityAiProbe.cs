using System;
using System.Linq;
using System.Reflection;

namespace Mosaic.Bridge.Tools.AI
{
    /// <summary>
    /// Detects whether Unity AI's asset generation is present in this Editor, without
    /// taking a compile-time dependency on it.
    ///
    /// Reflection rather than an asmdef reference is deliberate, for two reasons:
    /// Unity AI is entitlement-gated (it only resolves for a signed-in account with AI
    /// access, and adding it to manifest.json breaks that resolution), and Unity has
    /// renamed these namespaces more than once. A missing or renamed type must degrade
    /// to "unavailable", never break the build or throw.
    ///
    /// The bridge does not invoke generation itself — see AiGenerateAssetTool for why.
    /// This probe only answers "can the caller expect Unity AI to be there?".
    /// </summary>
    internal static class UnityAiProbe
    {
        // Ordered most- to least-likely. Kept deliberately broad because the exact
        // entry type has moved between releases.
        private static readonly string[] CandidateTypes =
        {
            "Unity.AI.Generators.Tools.AssetGenerators",
            "Unity.AI.Generators.AssetGenerators",
            "Unity.AI.Generators.Editor.AssetGenerators",
        };

        private static readonly string[] CandidateAssemblies =
        {
            "Unity.AI.Generators",
            "Unity.AI.Generators.Editor",
        };

        /// <summary>
        /// True when Unity AI generation appears usable. <paramref name="detail"/> carries
        /// the matched type or assembly for diagnostics, or null when nothing matched.
        /// Never throws.
        /// </summary>
        internal static bool IsAvailable(out string detail)
        {
            detail = null;
            try
            {
                foreach (var name in CandidateTypes)
                {
                    var t = FindType(name);
                    if (t != null)
                    {
                        detail = $"{name} ({t.Assembly.GetName().Name})";
                        return true;
                    }
                }

                // Fall back to assembly presence: the package may be installed with an
                // entry point we do not know the name of.
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => CandidateAssemblies.Contains(SafeName(a)));
                if (asm != null)
                {
                    detail = $"assembly {SafeName(asm)}";
                    return true;
                }
            }
            catch
            {
                // Probing must never be fatal — treat any failure as "not available".
            }

            return false;
        }

        private static string SafeName(Assembly a)
        {
            try { return a.GetName().Name; } catch { return null; }
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName, throwOnError: false);
                    if (t != null) return t;
                }
                catch
                {
                    // Some dynamic assemblies throw on GetType; skip them.
                }
            }
            return null;
        }
    }
}
