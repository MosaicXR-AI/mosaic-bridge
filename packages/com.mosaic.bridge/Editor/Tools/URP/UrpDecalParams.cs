#if MOSAIC_HAS_URP
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.URP
{
    public sealed class UrpDecalParams
    {
        /// <summary>Name of the decal projector GameObject.</summary>
        [Required] public string Name { get; set; }

        /// <summary>Path to the decal material asset (e.g. "Assets/Materials/MyDecal.mat").</summary>
        public string MaterialPath { get; set; }

        /// <summary>Size of the decal projector [width, height, depth]. Default [1,1,1].</summary>
        public float[] Size { get; set; }

        /// <summary>World position [x, y, z]. Default [0,0,0].</summary>
        public float[] Position { get; set; }

        /// <summary>Euler rotation [x, y, z]. Default [0,0,0].</summary>
        public float[] Rotation { get; set; }
    }
}
#endif
