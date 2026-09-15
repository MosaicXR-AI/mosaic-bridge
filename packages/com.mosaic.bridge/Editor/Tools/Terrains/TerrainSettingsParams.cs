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

        /// <summary>Whether this terrain tile auto-connects to adjacent tiles sharing the same GroupingID.</summary>
        public bool? AllowAutoConnect { get; set; }
        /// <summary>Tiles with the same GroupingID auto-connect when AllowAutoConnect is true.</summary>
        public int? GroupingId { get; set; }

        /// <summary>Asset path of a custom Material to render the terrain with. Empty string reverts to the built-in default.</summary>
        public string MaterialTemplatePath { get; set; }

        /// <summary>Enables the terrain instance renderer (GPU instancing for terrain patches).</summary>
        public bool? DrawInstanced { get; set; }

        /// <summary>Multiplier applied to the current LOD bias when rendering LOD trees (SpeedTree).</summary>
        public float? TreeLodBiasMultiplier { get; set; }

        /// <summary>Rendering layer mask bits (URP/HDRP Rendering Layers) this terrain's renderer lives on.</summary>
        public uint? RenderingLayerMask { get; set; }
    }
}
