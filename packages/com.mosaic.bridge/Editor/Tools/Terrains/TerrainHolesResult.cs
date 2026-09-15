namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainHolesResult
    {
        public string Action { get; set; }
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public int HolesResolution { get; set; }
        public string Message { get; set; }

        /// <summary>get only: flat row-major [Width*HeightCells]. true = surface, false = hole.</summary>
        public bool[] Holes { get; set; }
    }
}
