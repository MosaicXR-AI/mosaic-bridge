using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingLightProbesParams
    {
        /// <summary>Action: create, grid, add-positions, clear.</summary>
        [Required] public string Action { get; set; }

        /// <summary>GameObject name of the LightProbeGroup. Required for grid/add-positions/clear;
        /// used as the new GameObject's name for create.</summary>
        [Required] public string Name { get; set; }

        // -- create --
        public float[] Position { get; set; }

        // -- grid --
        /// <summary>World-space [x, y, z] min/max corners of the region to fill.</summary>
        public float[] BoundsMin { get; set; }
        public float[] BoundsMax { get; set; }
        /// <summary>Grid spacing in world units, per axis [x, y, z].</summary>
        public float[] Spacing { get; set; }
        /// <summary>When true, for each XZ grid cell, raycast down from BoundsMax.y to find the
        /// ground surface and place the probe at groundHeight + HeightOffset instead of filling Y
        /// by Spacing.y — keeps probes just above the actual floor instead of floating in open air.</summary>
        public bool RaycastAboveGround { get; set; }
        public float HeightOffset { get; set; } = 0.1f;
        /// <summary>Layer names (comma-separated) the ground raycast hits. Empty means all layers.</summary>
        public string GroundLayerMask { get; set; }

        // -- add-positions --
        /// <summary>Flat array of positions to append: [x1,y1,z1, x2,y2,z2, ...].</summary>
        public float[] Positions { get; set; }
    }
}
