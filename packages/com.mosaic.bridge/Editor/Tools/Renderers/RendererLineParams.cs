using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Renderers
{
    public sealed class RendererLineParams
    {
        public string Name { get; set; }
        public int? InstanceId { get; set; }

        /// <summary>Flattened [x0,y0,z0, x1,y1,z1, ...] world- or local-space (see UseWorldSpace) positions.
        /// At least two points (6 floats) are required.</summary>
        public float[] Positions { get; set; }

        public bool UseWorldSpace { get; set; } = true;
        public bool Loop { get; set; }

        public float? StartWidth { get; set; }
        public float? EndWidth { get; set; }
        /// <summary>Keyframe times/values (0..1 normalized along the line) for a varying width. Overrides Start/EndWidth.</summary>
        public float[] WidthCurveTimes { get; set; }
        public float[] WidthCurveValues { get; set; }

        public float[] StartColor { get; set; }
        public float[] EndColor { get; set; }

        public string MaterialPath { get; set; }

        /// <summary>"View" (always faces camera) or "TransformZ" (fixed to the object's transform).</summary>
        public string Alignment { get; set; }
        /// <summary>"Stretch", "Tile", "DistributePerSegment", or "RepeatPerSegment".</summary>
        public string TextureMode { get; set; }
    }
}
