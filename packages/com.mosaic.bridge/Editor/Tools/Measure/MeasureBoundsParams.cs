using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>Parameters for measure/bounds tool.</summary>
    public sealed class MeasureBoundsParams
    {
        /// <summary>Name of the GameObject to measure.</summary>
        [Required] public string GameObjectName { get; set; }

        /// <summary>Bounds mode: "aabb" (default), "obb", "renderer", "collider", "mesh".</summary>
        public string Mode { get; set; } = "aabb";

        /// <summary>Merge children's bounds (default true).</summary>
        public bool IncludeChildren { get; set; } = true;

        /// <summary>Output unit: "meters" (default), "centimeters", "millimeters", "feet", "inches".</summary>
        public string Unit { get; set; } = "meters";

        /// <summary>If true, creates a wireframe visual box GameObject.</summary>
        public bool CreateVisual { get; set; } = false;

        /// <summary>RGBA color for the wireframe (default cyan).</summary>
        public float[] VisualColor { get; set; } = new float[] { 0f, 1f, 1f, 1f };
    }
}
