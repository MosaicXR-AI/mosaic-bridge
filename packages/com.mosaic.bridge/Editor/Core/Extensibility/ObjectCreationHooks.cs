using System;
using UnityEngine;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Core.Extensibility
{
    /// <summary>
    /// The one seam gameobject/create, probuilder/create and asset/instantiate_prefab share
    /// for object-quality checking, without the free Bridge depending on Pro.
    /// </summary>
    /// <remarks>
    /// The dependency has to run this direction: Pro already references Bridge; Bridge
    /// referencing Pro back would mean the open-source package could not compile, or ship,
    /// without the commercial one. So Bridge exposes an empty extension point — a settable
    /// delegate, not an interface Pro implements and Bridge discovers, because there is
    /// exactly one expected subscriber and a delegate is the whole mechanism needed for
    /// that. Mosaic.Pro.Core's own bootstrap sets <see cref="Provider"/> at load time,
    /// through the same [InitializeOnLoad] self-registration pattern used everywhere else
    /// Pro extends the Bridge. With Pro absent, <see cref="Provider"/> stays null and every
    /// creation tool works exactly as it always has — this is additive, never a
    /// prerequisite.
    /// </remarks>
    public static class ObjectCreationHooks
    {
        /// <summary>
        /// Set by Mosaic.Pro.Core when it is installed and licensed. Null otherwise.
        /// Takes the created object and the route name that made it ("gameobject/create"),
        /// returns the report to attach to that tool's result.
        /// </summary>
        public static Func<GameObject, string, ObjectQaReport> Provider;

        /// <summary>
        /// Runs the provider if one is installed. Never throws: a defect in an optional,
        /// paid quality check must never be able to break the free tool call that triggered
        /// it. A provider exception becomes a "not_available" report with the exception
        /// message, not a failure of the creation tool itself.
        /// </summary>
        public static ObjectQaReport TryRun(GameObject go, string routeName)
        {
            var provider = Provider;
            if (provider == null) return null;
            try
            {
                return provider(go, routeName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Mosaic.Bridge] Object quality check threw and was skipped: {e.Message}");
                return new ObjectQaReport
                {
                    Status = "not_available",
                    Violations = Array.Empty<string>(),
                    Instruction = $"The quality check itself failed ({e.Message}); the object was created normally.",
                };
            }
        }
    }
}
