namespace Mosaic.Bridge.Tools.Tilemap
{
    public sealed class TilemapCreateParams
    {
        /// <summary>New Tilemap layer's GameObject name, e.g. "Ground", "Background", "Hazards".</summary>
        public string Name { get; set; }

        /// <summary>Reuse an existing Grid (for a second/third layer that must share cell size and
        /// layout) — by name. Ignored if GridInstanceId is set.</summary>
        public string GridName { get; set; }

        /// <summary>Reuse an existing Grid by InstanceId.</summary>
        public int? GridInstanceId { get; set; }

        // ── Grid params — only applied when a NEW Grid is created ──────────────

        /// <summary>Rectangular (default), Hexagon, Isometric, or IsometricZAsY.</summary>
        public string CellLayout { get; set; }

        /// <summary>[x, y, z]. Defaults to Unity's own Grid default (1,1,1) if omitted.</summary>
        public float[] CellSize { get; set; }

        /// <summary>Required alongside CellLayout=Isometric/IsometricZAsY: XYZ, XZY, YXZ, YZX, ZXY, ZYX.</summary>
        public string CellSwizzle { get; set; }

        // ── TilemapRenderer params ───────────────────────────────────────────

        public string SortingLayerName { get; set; }
        public int SortingOrder { get; set; }

        /// <summary>Chunk (default, batched) or Individual (one draw call per tile — needed for
        /// per-tile animation/shaders).</summary>
        public string Mode { get; set; }
    }
}
