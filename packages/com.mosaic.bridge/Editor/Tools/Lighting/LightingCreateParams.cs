using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingCreateParams
    {
        [Required] public string Type { get; set; }  // Directional, Point, Spot, Area
        public string Name { get; set; }              // null defaults to "{Type} Light"
        public float[] Color { get; set; }            // [r,g,b] or [r,g,b,a] 0-1 range
        public float? Intensity { get; set; }
        public float? Range { get; set; }             // Point / Spot only
        public float? SpotAngle { get; set; }         // Spot only
        public float[] Position { get; set; }         // [x,y,z]
        public float[] Rotation { get; set; }         // euler angles [x,y,z]

        /// <summary>"Realtime", "Mixed", or "Baked". Editor-only. Null leaves Unity's default.</summary>
        public string LightmapBakeType { get; set; }
        /// <summary>Asset path of a Texture to project as a cookie.</summary>
        public string CookiePath { get; set; }
        public float[] CookieSize { get; set; } // [width, height] — Light.cookieSize2D
        /// <summary>Layer names (comma-separated) this light affects. Null leaves Unity's default.</summary>
        public string CullingMask { get; set; }
        public float? ShadowBias { get; set; }
        /// <summary>[width, height] for Area lights.</summary>
        public float[] AreaSize { get; set; }
    }
}
