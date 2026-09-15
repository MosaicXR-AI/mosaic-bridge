using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainHolesParams
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }

        /// <summary>Action: rect, circle, array, clear, get.</summary>
        [Required] public string Action { get; set; }

        // -- rect --

        /// <summary>Holesmap pixel coordinates of the rectangle's corner.</summary>
        public int RectX { get; set; }
        public int RectY { get; set; }
        public int RectWidth { get; set; }
        public int RectHeight { get; set; }

        // -- circle --

        public int CenterX { get; set; }
        public int CenterY { get; set; }
        public int Radius { get; set; }

        // -- array / get --

        public int ArrayX { get; set; }
        public int ArrayY { get; set; }
        public int Width { get; set; }
        public int HeightCells { get; set; }
        /// <summary>array only: flat row-major [HeightCells*Width]. true = surface (solid), false = hole.</summary>
        public bool[] Holes { get; set; }
    }
}
