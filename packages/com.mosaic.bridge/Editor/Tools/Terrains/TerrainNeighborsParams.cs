namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainNeighborsParams
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }

        /// <summary>Name of the terrain GameObject in the negative-X direction. Null/empty leaves that side unset.</summary>
        public string LeftName { get; set; }
        /// <summary>Name of the terrain GameObject in the positive-Z direction.</summary>
        public string TopName { get; set; }
        /// <summary>Name of the terrain GameObject in the positive-X direction.</summary>
        public string RightName { get; set; }
        /// <summary>Name of the terrain GameObject in the negative-Z direction.</summary>
        public string BottomName { get; set; }
    }
}
