namespace Mosaic.Bridge.Tools.DataViz
{
    public sealed class ViewExplodeParams
    {
        /// <summary>Name of the parent GameObject of the assembly to explode (required).</summary>
        public string RootGameObject { get; set; }

        /// <summary>Multiplier for displacement. 0 = no explosion, 1 = parts just separated, >1 = spread further. Default 1.5.</summary>
        public float ExplosionFactor { get; set; } = 1.5f;

        /// <summary>Direction mode: "radial" (default), "axis_x", "axis_y", "axis_z", "custom".</summary>
        public string Direction { get; set; } = "radial";

        /// <summary>Custom per-part axis vector used when Direction == "custom". Length 3.</summary>
        public float[] CustomDirection { get; set; }

        /// <summary>Part selection strategy: "direct_children" (default, immediate children), "all_renderers" (every renderer in hierarchy), "by_layer" (group by Unity layer).</summary>
        public string Strategy { get; set; } = "direct_children";

        /// <summary>When true, generate a runtime MonoBehaviour that tweens to exploded positions. Default false (apply immediately).</summary>
        public bool Animate { get; set; } = false;

        /// <summary>Seconds for tween duration when Animate == true. Default 1.5.</summary>
        public float Duration { get; set; } = 1.5f;

        /// <summary>When true (default), use the combined bounds center as the origin of explosion. When false, use the root transform.position.</summary>
        public bool UseBoundsCenter { get; set; } = true;

        /// <summary>Optional save path for the generated animator script (only used when Animate == true). Default "Assets/Generated/DataViz/".</summary>
        public string SavePath { get; set; }
    }
}
