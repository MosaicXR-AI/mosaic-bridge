using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Tilemap
{
    public sealed class TilemapSetTilesParams
    {
        public int? TilemapInstanceId { get; set; }
        public string TilemapName { get; set; }

        /// <summary>ASCII map mode: rows top-to-bottom (row 0 = highest Y), one character per cell,
        /// left-to-right = increasing X. ' ' and '.' always mean "no tile" and need no Legend entry.
        /// Requires Legend. Mutually exclusive with Cells.</summary>
        public string[] AsciiMap { get; set; }

        /// <summary>Maps each AsciiMap character (as a 1-character string key) to a Tile asset path.</summary>
        public Dictionary<string, string> Legend { get; set; }

        /// <summary>Explicit-cells mode: paint exactly these cells. Mutually exclusive with AsciiMap.</summary>
        public TileCellParam[] Cells { get; set; }

        /// <summary>Clears every existing tile on this Tilemap before painting. Default false.</summary>
        public bool ClearFirst { get; set; }
    }

    public sealed class TileCellParam
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string TileAssetPath { get; set; }
    }
}
