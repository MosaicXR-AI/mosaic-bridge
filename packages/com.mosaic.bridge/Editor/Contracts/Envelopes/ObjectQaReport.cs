namespace Mosaic.Bridge.Contracts.Envelopes
{
    /// <summary>
    /// Attached to the result of every object-creation tool (gameobject/create,
    /// probuilder/create, asset/instantiate_prefab) when an object-quality provider is
    /// installed. Null when none is (the free Bridge alone): a creation tool's result
    /// always has this field, but only ever populates it when something is listening.
    /// </summary>
    /// <remarks>
    /// The check runs in two stages, because only the first is something code can actually
    /// verify. "Is the collider realistic, is the pivot sane" is deterministic — bounds
    /// math, component presence, convexity rules Unity itself enforces. "Does the shape
    /// look real, does it fit the scene" is a visual judgment call, and no C# run inside
    /// the object-creation call can make it: that judgment belongs to whichever agent is
    /// actually looking at the screenshots, which is the caller, not the Editor.
    ///
    /// So: <see cref="Status"/> is "failed" only for the deterministic half — a real,
    /// checkable defect, and the creation tool call itself fails when this is set, with
    /// the object left in the scene for a human or agent to fix or remove. When the
    /// deterministic checks pass, <see cref="Status"/> is "pending_visual_check": the
    /// creation call still succeeds, but <see cref="CapturePaths"/> and
    /// <see cref="Instruction"/> ask the caller to look at what was actually built and,
    /// if it judges something wrong, call the companion verdict tool.
    /// </remarks>
    public sealed class ObjectQaReport
    {
        /// <summary>"failed" | "pending_visual_check" | "not_available".</summary>
        public string Status { get; set; }

        /// <summary>One line per deterministic defect found. Empty when none were.</summary>
        public string[] Violations { get; set; }

        /// <summary>Absolute paths to the angle screenshots, only when Status is
        /// "pending_visual_check" — there is nothing worth looking at otherwise.</summary>
        public string[] CapturePaths { get; set; }

        /// <summary>What the caller should do next, in plain language.</summary>
        public string Instruction { get; set; }

        /// <summary>Correlates a later object/qa-verdict call back to this object and its
        /// captures. Present only alongside "pending_visual_check".</summary>
        public string QaId { get; set; }
    }
}
