namespace Mosaic.Bridge.Tools.DataViz
{
    public sealed class ViewExplodeResult
    {
        /// <summary>Name of the root GameObject that was exploded.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Number of parts whose positions were affected.</summary>
        public int AffectedPartCount { get; set; }

        /// <summary>Resolved explosion factor.</summary>
        public float ExplosionFactor { get; set; }

        /// <summary>Resolved part-selection strategy (lower-case).</summary>
        public string Strategy { get; set; }

        /// <summary>Asset path of the generated animator script (populated when Animate == true).</summary>
        public string ScriptPath { get; set; }
    }
}
