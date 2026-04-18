namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainSettingsParams
    {
        public int    InstanceId { get; set; }
        public string Name       { get; set; }

        /// <summary>Maximum distance at which terrain textures will be rendered at full resolution.</summary>
        public float? BasemapDistance { get; set; }

        /// <summary>Maximum distance at which detail objects are drawn.</summary>
        public float? DetailObjectDistance { get; set; }

        /// <summary>Distance at which detail objects start fading out.</summary>
        public float? DetailObjectDensity { get; set; }

        /// <summary>Maximum distance at which trees are drawn.</summary>
        public float? TreeDistance { get; set; }

        /// <summary>Maximum distance at which tree billboards are used instead of full meshes.</summary>
        public float? TreeBillboardDistance { get; set; }

        /// <summary>Maximum number of mesh trees rendered at one time.</summary>
        public int? TreeMaximumFullLODCount { get; set; }

        /// <summary>Pixel error rate for rendering the terrain. Lower values = higher quality.</summary>
        public float? HeightmapPixelError { get; set; }

        /// <summary>Whether the terrain casts shadows.</summary>
        public bool? CastShadows { get; set; }

        /// <summary>Whether the terrain draws.</summary>
        public bool? DrawHeightmap { get; set; }

        /// <summary>Whether tree and detail objects are drawn.</summary>
        public bool? DrawTreesAndFoliage { get; set; }
    }
}
