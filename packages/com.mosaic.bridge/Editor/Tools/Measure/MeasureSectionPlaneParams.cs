using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>Parameters for measure/section-plane tool (Story 33-5).</summary>
    public sealed class MeasureSectionPlaneParams
    {
        /// <summary>Point on the section plane (world space).</summary>
        [Required] public float[] Position { get; set; }

        /// <summary>Plane normal (world space). Must be non-zero.</summary>
        [Required] public float[] Normal { get; set; }

        /// <summary>Optional list of GameObject names to clip. If null/empty, all renderers in scene are clipped.</summary>
        public string[] Targets { get; set; }

        /// <summary>If true, fills the cut face with a solid color overlay. Default true.</summary>
        public bool CapSurface { get; set; } = true;

        /// <summary>RGBA color of the cap surface. Default orange [1, 0.5, 0, 1].</summary>
        public float[] CapColor { get; set; } = new float[] { 1f, 0.5f, 0f, 1f };

        /// <summary>If true, the runtime script LERPs the plane position for a sweep effect.</summary>
        public bool Animate { get; set; } = false;

        /// <summary>Animation duration in seconds. Default 2.0.</summary>
        public float AnimateDuration { get; set; } = 2.0f;

        /// <summary>Optional end position for the sweep animation.</summary>
        public float[] AnimateEndPosition { get; set; }

        /// <summary>Optional name override for the created plane GameObject.</summary>
        public string Name { get; set; }
    }
}
