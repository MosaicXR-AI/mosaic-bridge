namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>Result for measure/section-plane tool (Story 33-5).</summary>
    public sealed class MeasureSectionPlaneResult
    {
        /// <summary>InstanceID of the created plane controller GameObject.</summary>
        public int PlaneId { get; set; }

        /// <summary>Name of the created plane GameObject.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Number of renderers affected by clipping.</summary>
        public int ClippedObjectCount { get; set; }

        /// <summary>Final plane position (world space).</summary>
        public float[] Position { get; set; }

        /// <summary>Final plane normal (world space, normalized).</summary>
        public float[] Normal { get; set; }

        /// <summary>Asset path of the generated SectionPlaneRuntime.cs script (relative to project root).</summary>
        public string GeneratedScriptPath { get; set; }
    }
}
