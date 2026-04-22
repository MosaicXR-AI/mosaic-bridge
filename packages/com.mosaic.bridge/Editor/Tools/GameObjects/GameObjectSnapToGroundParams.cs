namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectSnapToGroundParams
    {
        /// <summary>Name of the GameObject to snap. Supports hierarchy paths like "Parent/Child".</summary>
        public string GameObjectPath { get; set; }

        /// <summary>Instance ID. Preferred over GameObjectPath when provided.</summary>
        public int InstanceId { get; set; }

        /// <summary>Additional Y offset above ground (default 0.05 — avoids z-fighting).</summary>
        public float YOffset { get; set; } = 0.05f;

        /// <summary>
        /// "terrain" (default) — samples Unity Terrain.SampleHeight.
        /// "raycast" — fires a downward physics ray; works for non-terrain ground (floors, meshes).
        /// </summary>
        public string SnapMode { get; set; } = "terrain";

        /// <summary>Terrain name for SnapMode=terrain. Defaults to Terrain.activeTerrain when omitted.</summary>
        public string TerrainName { get; set; }

        /// <summary>Raycast layer mask (SnapMode=raycast only). -1 = all layers.</summary>
        public int LayerMask { get; set; } = -1;
    }
}
