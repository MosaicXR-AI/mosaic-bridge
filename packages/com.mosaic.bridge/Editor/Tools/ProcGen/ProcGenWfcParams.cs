using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenWfcParams
    {
        [Required] public int                   Width          { get; set; }
        [Required] public int                   Height         { get; set; }
        public int                              Depth          { get; set; } = 1;
        public int?                             Seed           { get; set; }
        public int                              BacktrackLimit { get; set; } = 1000;
        [Required] public List<TileDefinition>  Tiles          { get; set; }
        public List<PrefabMapping>              PrefabMapping  { get; set; }
        public float                            CellSize       { get; set; } = 1.0f;
        public string                           ParentObject   { get; set; }
    }

    public sealed class TileDefinition
    {
        [Required] public string                          Id               { get; set; }
        public float                                      Weight           { get; set; } = 1.0f;
        public Dictionary<string, string[]>               AllowedNeighbors { get; set; }
    }

    public sealed class PrefabMapping
    {
        [Required] public string TileId     { get; set; }
        [Required] public string PrefabPath { get; set; }
    }
}
